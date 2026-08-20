using Microsoft.EntityFrameworkCore;
using Orbit.Application.Abstractions;
using Orbit.Domain.Idempotency;

namespace Orbit.Infrastructure.Persistence;

internal sealed class IdempotencyRecordRepository(OrbitDbContext dbContext) : IIdempotencyRecordRepository
{
    // ON CONFLICT DO UPDATE ... WHERE reclaims an expired row as a fresh reservation in the same
    // statement; when the existing row is still live the WHERE predicate fails, no row changes, and
    // the affected-row count below comes back 0 - exactly like ON CONFLICT DO NOTHING, but without a
    // second read to distinguish "no conflict" from "expired conflict". This resolves the concurrent-
    // replay race entirely inside Postgres (statement blocks on the conflicting row's lock until the
    // other transaction commits or rolls back) rather than an in-process lock, so it holds correctly
    // across the multiple API replicas this app runs behind.
    public async Task<bool> TryReserveAsync(
        Guid tenantId,
        string idempotencyKey,
        string requestPath,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO idempotency_records
                (id, tenant_id, idempotency_key, request_path, created_at, expires_at)
            VALUES
                ({Guid.CreateVersion7()}, {tenantId}, {idempotencyKey}, {requestPath}, {now}, {expiresAt})
            ON CONFLICT (tenant_id, idempotency_key, request_path) DO UPDATE
            SET id = EXCLUDED.id,
                created_at = EXCLUDED.created_at,
                expires_at = EXCLUDED.expires_at,
                response_status_code = NULL,
                response_body = NULL,
                response_content_type = NULL,
                completed_at = NULL
            WHERE idempotency_records.expires_at < EXCLUDED.created_at
            """,
            cancellationToken);

        return affected == 1;
    }

    public Task<IdempotencyRecord?> GetAsync(
        Guid tenantId,
        string idempotencyKey,
        string requestPath,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.TenantId == tenantId
                    && record.IdempotencyKey == idempotencyKey
                    && record.RequestPath == requestPath
                    && record.ExpiresAt > now,
                cancellationToken);

    public async Task CompleteAsync(
        Guid tenantId,
        string idempotencyKey,
        string requestPath,
        int statusCode,
        string? responseBody,
        string? responseContentType,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE idempotency_records
            SET response_status_code = {statusCode},
                response_body = {responseBody},
                response_content_type = {responseContentType},
                completed_at = {now}
            WHERE tenant_id = {tenantId}
                AND idempotency_key = {idempotencyKey}
                AND request_path = {requestPath}
            """,
            cancellationToken);
}

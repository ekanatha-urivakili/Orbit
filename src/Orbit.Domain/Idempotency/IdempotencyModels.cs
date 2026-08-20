namespace Orbit.Domain.Idempotency;

/// <summary>
/// Records the outcome of a client-supplied <c>Idempotency-Key</c> header on a mutating endpoint,
/// so a network retry that replays the same key returns the original response instead of
/// re-executing the mutation. Tenant-scoped and forced-RLS like other per-tenant tables.
/// <see cref="Orbit.Api.Idempotency.IdempotencyKeyFilter"/> reads/writes this via raw SQL
/// (<c>INSERT ... ON CONFLICT</c>) rather than the change tracker - see
/// <c>IdempotencyRecordRepository</c> for why: the unique constraint on
/// (tenant_id, idempotency_key, request_path) is the concurrency guard for two in-flight requests
/// racing on the same key, not an in-process lock, since this app runs multiple replicas.
/// </summary>
public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestPath { get; private set; } = string.Empty;
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public string? ResponseContentType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
}

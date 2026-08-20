using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Orbit.Application.Abstractions;

namespace Orbit.Api.Idempotency;

/// <summary>
/// Wraps a POST creation endpoint with standard <c>Idempotency-Key</c> replay semantics: a client
/// that supplies the header on a retried request gets back the original response instead of
/// re-executing the mutation. Applied per-endpoint via <c>.AddEndpointFilter&lt;IdempotencyKeyFilter&gt;()</c>
/// rather than globally, so MediatR handlers stay unaware of HTTP-level retry concerns.
///
/// Concurrency: the reservation race between two in-flight requests replaying the same key is
/// resolved by <see cref="IIdempotencyRecordRepository.TryReserveAsync"/>'s DB unique constraint,
/// not an in-process lock - see that repository for why this is safe across replicas. A losing
/// request here returns 409 rather than blocking, since Postgres itself already blocked this
/// statement until the winner's transaction committed or rolled back.
///
/// Scope: only requests carrying the header are affected; requests without one execute normally.
/// Only successful (non-exception) responses are memoized - a thrown exception (e.g. validation
/// failure) unwinds through <c>TenantTransactionMiddleware</c>, which rolls back the whole request
/// transaction including this filter's reservation row, so the key is free to retry.
/// </summary>
public sealed class IdempotencyKeyFilter(TimeProvider timeProvider) : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";

    // Standard idempotency-key retry window (e.g. Stripe uses 24h); long enough to cover client
    // retry/backoff strategies, short enough to bound table growth without a cleanup job yet.
    private static readonly TimeSpan RecordLifetime = TimeSpan.FromHours(24);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return await next(context);
        }

        var idempotencyKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 255)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/problems/invalid-idempotency-key",
                title: "Idempotency-Key must be 1 to 255 characters when supplied.");
        }

        var tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        var repository = httpContext.RequestServices.GetRequiredService<IIdempotencyRecordRepository>();
        var requestPath = httpContext.Request.Path.Value ?? string.Empty;
        var now = timeProvider.GetUtcNow();

        var reserved = await repository.TryReserveAsync(
            tenantContext.TenantId, idempotencyKey, requestPath, now, now + RecordLifetime,
            httpContext.RequestAborted);

        if (!reserved)
        {
            var existing = await repository.GetAsync(
                tenantContext.TenantId, idempotencyKey, requestPath, now, httpContext.RequestAborted);

            if (existing is { CompletedAt: not null })
            {
                return Results.Content(
                    existing.ResponseBody,
                    existing.ResponseContentType,
                    statusCode: existing.ResponseStatusCode);
            }

            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/problems/idempotency-key-in-progress",
                title: "A request with this Idempotency-Key is already being processed.");
        }

        var result = await next(context);

        if (result is IStatusCodeHttpResult { StatusCode: { } statusCode } and IValueHttpResult valueResult)
        {
            var jsonOptions = httpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value;
            var responseBody = valueResult.Value is null
                ? null
                : JsonSerializer.Serialize(valueResult.Value, valueResult.Value.GetType(), jsonOptions.SerializerOptions);

            await repository.CompleteAsync(
                tenantContext.TenantId,
                idempotencyKey,
                requestPath,
                statusCode,
                responseBody,
                "application/json",
                timeProvider.GetUtcNow(),
                httpContext.RequestAborted);
        }

        return result;
    }
}

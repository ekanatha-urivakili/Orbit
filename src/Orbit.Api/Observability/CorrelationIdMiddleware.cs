using System.Diagnostics;

namespace Orbit.Api.Observability;

/// <summary>
/// Establishes the request's correlation id (a client/support-facing join key, distinct from the
/// automatic OpenTelemetry TraceId) and pushes both into the logging scope. Placed right after
/// UseForwardedHeaders and before UseExceptionHandler so it wraps auth, rate limiting, and
/// authorization - the header is present even when one of those short-circuits the pipeline.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;

        // OnStarting fires right before the first byte is written, after UseExceptionHandler
        // (downstream) has finished clearing/rewriting the response for a handled exception - a
        // header set before next(context) would otherwise be wiped by that rewrite.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString(),
        }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var header = context.Request.Headers[HeaderName].ToString();
        return Guid.TryParse(header, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
    }
}

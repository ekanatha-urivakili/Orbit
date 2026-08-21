using System.Diagnostics.Metrics;

namespace Orbit.Application.Caching;

/// <summary>
/// Fail-open counter for every HybridCache-backed consumer added under
/// OBSERVABILITY-CACHING-ARCHITECTURE.md §5 (see CacheFailOpen). Follows RateLimitTelemetry's exact
/// pattern: a static Meter registered by name via .AddMeter(CacheTelemetry.MeterName) in
/// Program.cs. Hit/miss counts are not duplicated here - HybridCache already publishes those via
/// its own built-in "Microsoft.Extensions.Caching.Hybrid" meter, wired in the same OTel setup.
/// </summary>
public static class CacheTelemetry
{
    public const string MeterName = "Orbit.Caching";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> FailOpenTotal =
        Meter.CreateCounter<long>("cache_fail_open_total");
}

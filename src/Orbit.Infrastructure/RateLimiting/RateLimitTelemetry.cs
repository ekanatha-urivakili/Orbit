using System.Diagnostics.Metrics;

namespace Orbit.Infrastructure.RateLimiting;

/// <summary>
/// The rate-limit metrics named in ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §8.2/§13.7.1
/// (<c>rate_limit_check_latency_seconds</c>, <c>rate_limit_rejections_total</c>), wired as OTel
/// <see cref="Meter"/> instruments per §13.7.2 step 5.
/// </summary>
public static class RateLimitTelemetry
{
    public const string MeterName = "Orbit.RateLimiting";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<double> CheckLatencySeconds =
        Meter.CreateHistogram<double>("rate_limit_check_latency_seconds", unit: "s");

    public static readonly Counter<long> RejectionsTotal =
        Meter.CreateCounter<long>("rate_limit_rejections_total");
}

namespace Orbit.Infrastructure.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting:Distributed";

    public bool Enabled { get; set; }
}

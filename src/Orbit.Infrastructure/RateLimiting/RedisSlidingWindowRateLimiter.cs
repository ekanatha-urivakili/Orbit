using System.Diagnostics;
using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Orbit.Infrastructure.RateLimiting;

/// <summary>
/// Sliding-window limiter shared across every API replica via a Valkey sorted set, so a caller
/// round-robined across N replicas is checked against one global count instead of getting up to
/// N times the configured limit from N independent in-memory <see cref="FixedWindowRateLimiter"/>
/// instances. See ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §13.7.1 (ADR-022).
/// Fails open (permits the request) on Valkey unavailability, matching this codebase's standing
/// rule that cache failure degrades performance, never correctness/availability.
/// </summary>
public sealed class RedisSlidingWindowRateLimiter(
    IConnectionMultiplexer connectionMultiplexer,
    string policyName,
    string partitionKey,
    TimeSpan window,
    int permitLimit) : RateLimiter
{
    private const string SlidingWindowScript = """
        local window_start = ARGV[3] - ARGV[1]
        redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, window_start)
        local count = redis.call('ZCARD', KEYS[1])
        if count < tonumber(ARGV[2]) then
            redis.call('ZADD', KEYS[1], ARGV[3], ARGV[3] .. '-' .. math.random())
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
            return 1
        end
        return 0
        """;

    private static readonly RateLimitLease AllowedLease = new BooleanRateLimitLease(isAcquired: true);
    private static readonly RateLimitLease DeniedLease = new BooleanRateLimitLease(isAcquired: false);

    private readonly RedisKey _redisKey = $"orbit:ratelimit:{partitionKey}";

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var database = connectionMultiplexer.GetDatabase();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var allowed = (int)database.ScriptEvaluate(
                SlidingWindowScript,
                [_redisKey],
                [(long)window.TotalMilliseconds, permitLimit, now]);
            return RecordAndBuildLease(allowed == 1, stopwatch);
        }
        catch (RedisException)
        {
            return RecordAndBuildLease(allowed: true, stopwatch);
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var database = connectionMultiplexer.GetDatabase();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var allowed = (int)await database.ScriptEvaluateAsync(
                SlidingWindowScript,
                [_redisKey],
                [(long)window.TotalMilliseconds, permitLimit, now]).WaitAsync(cancellationToken);
            return RecordAndBuildLease(allowed == 1, stopwatch);
        }
        catch (RedisException)
        {
            return RecordAndBuildLease(allowed: true, stopwatch);
        }
    }

    private RateLimitLease RecordAndBuildLease(bool allowed, Stopwatch stopwatch)
    {
        RateLimitTelemetry.CheckLatencySeconds.Record(
            stopwatch.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("policy", policyName));
        if (!allowed)
        {
            RateLimitTelemetry.RejectionsTotal.Add(1, new KeyValuePair<string, object?>("policy", policyName));
        }

        return allowed ? AllowedLease : DeniedLease;
    }

    private sealed class BooleanRateLimitLease(bool isAcquired) : RateLimitLease
    {
        public override bool IsAcquired { get; } = isAcquired;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}

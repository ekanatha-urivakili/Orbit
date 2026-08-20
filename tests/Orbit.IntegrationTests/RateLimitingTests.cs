using Orbit.Infrastructure.RateLimiting;
using StackExchange.Redis;

namespace Orbit.IntegrationTests;

public sealed class RateLimitingTests
{
    private static string ValkeyConnectionString =>
        Environment.GetEnvironmentVariable("VALKEY_CONNECTION") ?? "localhost:6379";

    [Fact]
    public async Task TwoReplicaInstances_SharingOneValkeyKey_EnforceOneGlobalLimit()
    {
        using var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(ValkeyConnectionString);
        var partitionKey = $"test:{Guid.NewGuid()}";
        var window = TimeSpan.FromSeconds(2);
        const int permitLimit = 3;

        // Two separate limiter instances against the same partition key simulate two API
        // replicas checking the same client's rate limit independently, per §13.7.1.
        var replicaOne = new RedisSlidingWindowRateLimiter(
            connectionMultiplexer, "test-policy", partitionKey, window, permitLimit);
        var replicaTwo = new RedisSlidingWindowRateLimiter(
            connectionMultiplexer, "test-policy", partitionKey, window, permitLimit);

        var results = new List<bool>();
        for (var i = 0; i < 5; i++)
        {
            var limiter = i % 2 == 0 ? replicaOne : replicaTwo;
            using var lease = await limiter.AcquireAsync(1);
            results.Add(lease.IsAcquired);
        }

        Assert.Equal(permitLimit, results.Count(acquired => acquired));
        Assert.Equal(5 - permitLimit, results.Count(acquired => !acquired));
    }

    [Fact]
    public async Task Limiter_AllowsAgain_AfterWindowElapses()
    {
        using var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(ValkeyConnectionString);
        var partitionKey = $"test:{Guid.NewGuid()}";
        var window = TimeSpan.FromMilliseconds(500);
        var limiter = new RedisSlidingWindowRateLimiter(
            connectionMultiplexer, "test-policy", partitionKey, window, permitLimit: 1);

        using (var firstLease = await limiter.AcquireAsync(1))
        {
            Assert.True(firstLease.IsAcquired);
        }

        using (var secondLease = await limiter.AcquireAsync(1))
        {
            Assert.False(secondLease.IsAcquired);
        }

        await Task.Delay(window + TimeSpan.FromMilliseconds(200));

        using var thirdLease = await limiter.AcquireAsync(1);
        Assert.True(thirdLease.IsAcquired);
    }
}

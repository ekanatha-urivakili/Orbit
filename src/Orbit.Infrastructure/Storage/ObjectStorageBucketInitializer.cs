using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orbit.Infrastructure.Storage;

/// <summary>
/// Ensures the configured bucket exists at startup. Required for local/self-hosted MinIO, which
/// (unlike a managed production bucket ops provisions out-of-band) has nothing else to create it —
/// <c>MINIO_DEFAULT_BUCKETS</c> does not auto-create it on the pinned image (verified against a
/// live local MinIO: the env var is present in the container but no bucket is created).
/// </summary>
internal sealed class ObjectStorageBucketInitializer(
    IOptions<ObjectStorageOptions> options,
    ILogger<ObjectStorageBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var client = new AmazonS3Client(
            settings.AccessKey,
            settings.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl,
                ForcePathStyle = settings.ForcePathStyle,
                Timeout = TimeSpan.FromSeconds(5),
                // AWSSDK.S3 4.x NullReferenceExceptions inside ListBucketsAsync's region-resolution
                // path when ServiceURL points at a non-AWS endpoint with no AuthenticationRegion set
                // (reproduced against a live local MinIO) — set explicitly and use the bucket-scoped
                // exists/create calls below instead of ListBuckets, which hits the same bad path.
                AuthenticationRegion = "us-east-1",
            });

        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(client, settings.BucketName);
            if (!exists)
            {
                await client.PutBucketAsync(
                    new PutBucketRequest { BucketName = settings.BucketName }, cancellationToken);
                logger.LogInformation("Created object storage bucket {BucketName}.", settings.BucketName);
            }
        }
        catch (Exception exception)
        {
            // Best-effort: a managed production bucket may not grant this runtime credential
            // list/create permission, so a failure here must never block startup — only the
            // local/self-hosted MinIO path actually depends on this auto-create succeeding.
            logger.LogWarning(
                exception, "Could not verify or create object storage bucket {BucketName}.", settings.BucketName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

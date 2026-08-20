using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Storage;

/// <summary>
/// S3-compatible presigned-transfer storage adapter. The <see cref="AmazonS3Client"/> is
/// thread-safe and designed for reuse across requests, so this service is registered singleton.
/// </summary>
internal sealed class S3ObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly Protocol _protocol;

    public S3ObjectStorageService(IOptions<ObjectStorageOptions> options)
    {
        var settings = options.Value;
        _bucketName = settings.BucketName;
        // GetPreSignedUrlRequest.Protocol defaults to https regardless of client config, so it
        // must be set explicitly per-request to match the configured endpoint's real scheme —
        // otherwise a plain-HTTP local MinIO gets an unusable https:// presigned URL.
        _protocol = settings.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTP
            : Protocol.HTTPS;
        _client = new AmazonS3Client(
            settings.AccessKey,
            settings.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl,
                ForcePathStyle = settings.ForcePathStyle,
                AuthenticationRegion = "us-east-1",
            });
    }

    public PresignedUpload CreatePresignedUpload(string objectKey, string contentType, TimeSpan expiresIn)
    {
        var expiresAt = DateTime.UtcNow.Add(expiresIn);
        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Protocol = _protocol,
            Expires = expiresAt,
            ContentType = contentType,
        });

        return new PresignedUpload(url, objectKey, new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }

    public string CreatePresignedDownloadUrl(string objectKey, TimeSpan expiresIn) =>
        _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Protocol = _protocol,
            Expires = DateTime.UtcNow.Add(expiresIn),
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = "attachment",
            },
        });

    public string CreatePresignedDisplayUrl(string objectKey, TimeSpan expiresIn) =>
        _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Protocol = _protocol,
            Expires = DateTime.UtcNow.Add(expiresIn),
        });

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound
            || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            // PERF-04: Object was already removed from storage (manual cleanup, prior failure, etc.).
            // Treat as a no-op so callers can still clean up the database record.
        }
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        var response = await _client.GetObjectAsync(_bucketName, objectKey, cancellationToken);
        return response.ResponseStream;
    }

    public async Task MoveToQuarantineAsync(string objectKey, CancellationToken cancellationToken)
    {
        var quarantineKey = $"quarantine/{objectKey}";
        await _client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = _bucketName,
            SourceKey = objectKey,
            DestinationBucket = _bucketName,
            DestinationKey = quarantineKey,
        }, cancellationToken);
        await _client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken);
    }

    public void Dispose() => _client.Dispose();
}

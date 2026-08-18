namespace Orbit.Application.Abstractions;

public sealed record PresignedUpload(string UploadUrl, string ObjectKey, DateTimeOffset ExpiresAt);

/// <summary>
/// Presigned-transfer object storage port (MinIO locally, S3-compatible in production per
/// ARCH-ORBIT-001 §3.3). The API process never streams file bytes: it only mints time-limited
/// presigned URLs so the client uploads/downloads directly against the bucket.
/// </summary>
public interface IObjectStorageService
{
    PresignedUpload CreatePresignedUpload(string objectKey, string contentType, TimeSpan expiresIn);

    /// <summary>Forces <c>Content-Disposition: attachment</c> - for attachments the user explicitly downloads.</summary>
    string CreatePresignedDownloadUrl(string objectKey, TimeSpan expiresIn);

    /// <summary>No disposition override - for assets rendered inline (e.g. an <c>&lt;img&gt;</c> src) rather than downloaded.</summary>
    string CreatePresignedDisplayUrl(string objectKey, TimeSpan expiresIn);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

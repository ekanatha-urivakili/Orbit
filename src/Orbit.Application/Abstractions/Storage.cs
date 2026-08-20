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

    /// <summary>
    /// Server-side read of the object's bytes - unlike the presigned-URL methods above, this is
    /// used only by <c>AttachmentScanProcessor</c> to stream the file into the malware scanner; no
    /// other code path should read attachment bytes through the API/worker process.
    /// </summary>
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Moves an object to the quarantine prefix (copy then delete of the original key) after a
    /// malware scan flags it <c>Infected</c>. Quarantined objects are kept, not deleted outright,
    /// so an operator can inspect what was uploaded.
    /// </summary>
    Task MoveToQuarantineAsync(string objectKey, CancellationToken cancellationToken);
}

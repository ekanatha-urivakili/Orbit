namespace Orbit.Infrastructure.Storage;

/// <summary>
/// Bound from configuration section <c>ObjectStorage</c>. Points at MinIO for local development
/// and any S3-compatible bucket in production (ARCH-ORBIT-001 §3.3).
/// </summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "orbit";
    public string SecretKey { get; set; } = "orbit_local_secret";
    public string BucketName { get; set; } = "orbit-attachments";

    /// <summary>
    /// Path-style addressing (<c>host/bucket/key</c>) is required for MinIO and most
    /// self-hosted S3-compatible services; disable only for providers requiring virtual-hosted
    /// style (<c>bucket.host/key</c>).
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;
}

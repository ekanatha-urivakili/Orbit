namespace Orbit.Infrastructure.Scanning;

/// <summary>
/// Bound from configuration section <c>AttachmentScanning</c>. Points at a clamd instance reachable
/// over TCP (the <c>clamav/clamav</c> container in <c>deploy/podman/compose.yaml</c> locally).
/// <see cref="Enabled"/> defaults to <c>false</c> so an environment without clamd available (e.g. a
/// sandbox with no image-pull network access) still runs: <see cref="NoOpAttachmentScanner"/> is
/// registered instead of <see cref="ClamAvAttachmentScanner"/>, and attachments still flow through
/// the same Pending -> Clean state machine, just without a real scan.
/// </summary>
public sealed class AttachmentScanningOptions
{
    public const string SectionName = "AttachmentScanning";

    public bool Enabled { get; set; }
    public string ClamAvHost { get; set; } = "localhost";
    public int ClamAvPort { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 30;
}

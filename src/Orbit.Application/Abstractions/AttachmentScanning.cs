namespace Orbit.Application.Abstractions;

public enum AttachmentScanOutcome
{
    Clean,
    Infected,
    Failed,
}

public sealed record AttachmentScanResult(AttachmentScanOutcome Outcome, string? Detail)
{
    public static AttachmentScanResult Clean() => new(AttachmentScanOutcome.Clean, null);

    public static AttachmentScanResult Infected(string signature) =>
        new(AttachmentScanOutcome.Infected, signature);

    public static AttachmentScanResult Failed(string reason) =>
        new(AttachmentScanOutcome.Failed, reason);
}

/// <summary>
/// Malware-scans one attachment's bytes. <c>ClamAvAttachmentScanner</c> (Infrastructure) is the real
/// implementation, talking clamd's INSTREAM protocol over TCP; <c>NoOpAttachmentScanner</c> is the
/// fallback registered when <c>AttachmentScanning:Enabled</c> is false, for environments without a
/// clamd instance available. <c>AttachmentScanProcessor</c> is the only caller.
/// </summary>
public interface IAttachmentScanner
{
    Task<AttachmentScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken);
}

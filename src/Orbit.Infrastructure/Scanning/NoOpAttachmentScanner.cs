using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Scanning;

/// <summary>
/// Registered instead of <see cref="ClamAvAttachmentScanner"/> when <c>AttachmentScanning:Enabled</c>
/// is false. Always reports <see cref="AttachmentScanOutcome.Clean"/> so attachments still leave the
/// Pending state via the normal outbox pipeline in environments without a clamd instance to talk to.
/// </summary>
internal sealed class NoOpAttachmentScanner : IAttachmentScanner
{
    public Task<AttachmentScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken) =>
        Task.FromResult(AttachmentScanResult.Clean());
}

using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Domain.Tests;

public sealed class AttachmentTests
{
    private static (Guid TenantId, Guid WorkItemId, Guid UploaderId) NewIds() =>
        (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_AssignsFieldsFromInput()
    {
        var (tenantId, workItemId, uploaderId) = NewIds();
        var now = DateTimeOffset.UtcNow;

        var attachment = Attachment.Create(
            tenantId, workItemId, "diagram.png", "image/png", 2048, "tenant/item/key-diagram.png", uploaderId, now);

        Assert.NotEqual(Guid.Empty, attachment.Id);
        Assert.Equal(tenantId, attachment.TenantId);
        Assert.Equal(workItemId, attachment.WorkItemId);
        Assert.Equal("diagram.png", attachment.FileName);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Equal(2048, attachment.SizeBytes);
        Assert.Equal("tenant/item/key-diagram.png", attachment.ObjectKey);
        Assert.Equal(uploaderId, attachment.UploadedByMembershipId);
        Assert.Equal(now, attachment.UploadedAt);
        Assert.Equal(AttachmentScanStatus.Pending, attachment.ScanStatus);
        Assert.Null(attachment.ScannedAt);
    }

    [Fact]
    public void MarkScanned_TransitionsFromPendingOnce()
    {
        var (tenantId, workItemId, uploaderId) = NewIds();
        var attachment = Attachment.Create(
            tenantId, workItemId, "diagram.png", "image/png", 2048, "key", uploaderId, DateTimeOffset.UtcNow);
        var scannedAt = DateTimeOffset.UtcNow;

        attachment.MarkScanned(AttachmentScanStatus.Clean, scannedAt);

        Assert.Equal(AttachmentScanStatus.Clean, attachment.ScanStatus);
        Assert.Equal(scannedAt, attachment.ScannedAt);
        Assert.Throws<DomainException>(() => attachment.MarkScanned(AttachmentScanStatus.Infected, scannedAt));
    }

    [Fact]
    public void MarkScanned_RejectsPendingAsResult()
    {
        var (tenantId, workItemId, uploaderId) = NewIds();
        var attachment = Attachment.Create(
            tenantId, workItemId, "diagram.png", "image/png", 2048, "key", uploaderId, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => attachment.MarkScanned(AttachmentScanStatus.Pending, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_RejectsEmptyIdentifiers()
    {
        var (tenantId, workItemId, uploaderId) = NewIds();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<DomainException>(() =>
            Attachment.Create(Guid.Empty, workItemId, "file.png", "image/png", 100, "key", uploaderId, now));
        Assert.Throws<DomainException>(() =>
            Attachment.Create(tenantId, Guid.Empty, "file.png", "image/png", 100, "key", uploaderId, now));
        Assert.Throws<DomainException>(() =>
            Attachment.Create(tenantId, workItemId, "file.png", "image/png", 100, "key", Guid.Empty, now));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsBlankFileName(string fileName)
    {
        var (tenantId, workItemId, uploaderId) = NewIds();

        Assert.Throws<DomainException>(() =>
            Attachment.Create(tenantId, workItemId, fileName, "image/png", 100, "key", uploaderId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_RejectsFileNameOver255Characters()
    {
        var (tenantId, workItemId, uploaderId) = NewIds();
        var longName = new string('a', 256);

        Assert.Throws<DomainException>(() =>
            Attachment.Create(tenantId, workItemId, longName, "image/png", 100, "key", uploaderId, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25 * 1024 * 1024 + 1)]
    public void Create_RejectsSizeOutsideAllowedRange(long sizeBytes)
    {
        var (tenantId, workItemId, uploaderId) = NewIds();

        Assert.Throws<DomainException>(() =>
            Attachment.Create(tenantId, workItemId, "file.png", "image/png", sizeBytes, "key", uploaderId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_RejectsMissingObjectKey()
    {
        var (tenantId, workItemId, uploaderId) = NewIds();

        Assert.Throws<DomainException>(() =>
            Attachment.Create(tenantId, workItemId, "file.png", "image/png", 100, " ", uploaderId, DateTimeOffset.UtcNow));
    }
}

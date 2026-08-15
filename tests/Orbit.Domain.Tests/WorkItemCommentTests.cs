using Orbit.Domain.Common;
using Orbit.Domain.WorkItems;

namespace Orbit.Domain.Tests;

public sealed class WorkItemCommentTests
{
    private static WorkItemComment MakeComment(string body = "Initial comment body") =>
        WorkItemComment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            body,
            [],
            DateTimeOffset.UtcNow);

    [Fact]
    public void Create_AssignsIdAuthorAndVersion()
    {
        var tenantId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var authorMembershipId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var comment = WorkItemComment.Create(
            tenantId,
            workItemId,
            authorMembershipId,
            "First comment",
            [],
            now);

        Assert.NotEqual(Guid.Empty, comment.Id);
        Assert.Equal(tenantId, comment.TenantId);
        Assert.Equal(workItemId, comment.WorkItemId);
        Assert.Equal(authorMembershipId, comment.AuthorMembershipId);
        Assert.Equal("First comment", comment.Body);
        Assert.Equal(1, comment.Version);
        Assert.False(comment.IsDeleted);
        Assert.Null(comment.LastEditedAt);
        Assert.Equal(now, comment.CreatedAt);
    }

    [Fact]
    public void Create_TrimsBody()
    {
        var comment = WorkItemComment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  hello  ", [], DateTimeOffset.UtcNow);

        Assert.Equal("hello", comment.Body);
    }

    [Fact]
    public void Create_RejectsEmptyBody()
    {
        var action = () => WorkItemComment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "", [], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsBodyExceedingMaxLength()
    {
        var longBody = new string('x', 10_001);

        var action = () => WorkItemComment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), longBody, [], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Create_RejectsEmptyTenantId()
    {
        var action = () => WorkItemComment.Create(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "body", [], DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Edit_UpdatesBodyVersionAndEditedAt()
    {
        var comment = MakeComment();
        var editedAt = DateTimeOffset.UtcNow.AddMinutes(5);

        comment.Edit("Updated body", editedAt);

        Assert.Equal("Updated body", comment.Body);
        Assert.Equal(2, comment.Version);
        Assert.Equal(editedAt, comment.LastEditedAt);
        Assert.Equal(editedAt, comment.UpdatedAt);
    }

    [Fact]
    public void Edit_IsNoOpWhenBodyUnchanged()
    {
        var comment = MakeComment("Unchanged body");

        comment.Edit("Unchanged body", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(1, comment.Version);
        Assert.Null(comment.LastEditedAt);
    }

    [Fact]
    public void Edit_TrimsBodyBeforeComparison()
    {
        var comment = MakeComment("Trimmed body");

        // Whitespace-padded version of the same content — should be a no-op.
        comment.Edit("  Trimmed body  ", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(1, comment.Version);
    }

    [Fact]
    public void Edit_ThrowsOnDeletedComment()
    {
        var comment = MakeComment();
        comment.Delete(DateTimeOffset.UtcNow);

        var action = () => comment.Edit("New body", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Delete_SetsDeletionTimestamp()
    {
        var comment = MakeComment();
        var deletedAt = DateTimeOffset.UtcNow.AddMinutes(2);

        comment.Delete(deletedAt);

        Assert.True(comment.IsDeleted);
        Assert.Equal(deletedAt, comment.DeletedAt);
        Assert.Equal(2, comment.Version);
    }

    [Fact]
    public void Delete_IsIdempotent()
    {
        var comment = MakeComment();
        var first = DateTimeOffset.UtcNow.AddMinutes(1);
        var second = DateTimeOffset.UtcNow.AddMinutes(2);

        comment.Delete(first);
        comment.Delete(second);   // second call must not modify state

        Assert.Equal(first, comment.DeletedAt);
        Assert.Equal(2, comment.Version);  // incremented only once
    }
}

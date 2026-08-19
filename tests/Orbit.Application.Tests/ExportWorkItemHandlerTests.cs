using System.Text;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.WorkItems;

namespace Orbit.Application.Tests;

public sealed class ExportWorkItemHandlerTests
{
    [Theory]
    [InlineData(WorkItemExportFormat.Csv, "text/csv", ".csv")]
    [InlineData(WorkItemExportFormat.Xml, "application/xml", ".xml")]
    [InlineData(WorkItemExportFormat.Json, "application/json", ".json")]
    public async Task Handle_ProducesFileForEachFormat(WorkItemExportFormat format, string contentType, string extension)
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Export this card", null, WorkItemType.Task, Priority.Medium,
            DateTimeOffset.UtcNow);
        var handler = new ExportWorkItemHandler(new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem));

        var result = await handler.Handle(new ExportWorkItemQuery(workItem.Id, format), CancellationToken.None);

        Assert.Equal(contentType, result.ContentType);
        Assert.Equal($"ORB-1{extension}", result.FileName);
        Assert.Contains("Export this card", Encoding.UTF8.GetString(result.Content));
    }

    private sealed record TenantContextStub(Guid TenantId) : ITenantContext;

    private sealed class WorkItemRepositoryStub(WorkItem workItem) : IWorkItemRepository
    {
        public Task AddAsync(WorkItem value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkItem?> GetAsync(
            Guid tenantId, Guid workItemId, ProjectPermission permission, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(workItem.Id == workItemId && workItem.TenantId == tenantId ? workItem : null);
        public Task<PagedResult<WorkItem>> ListByProjectAsync(
            Guid tenantId, Guid projectId, ProjectPermission permission, int skip, int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WorkItem>([], 0));
        public Task<IReadOnlyList<WorkItem>> ListByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> workItemIds, ProjectPermission permission,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<bool> HasChildrenAsync(Guid tenantId, Guid parentWorkItemId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task RemoveAsync(WorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

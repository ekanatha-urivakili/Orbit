using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Application.WorkItems;
using Orbit.Domain.Access;
using Orbit.Domain.Choices;
using Orbit.Domain.Configuration;
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
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        var handler = new ExportWorkItemHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem), new WorkItemStatusRepositoryStub());

        var result = await handler.Handle(new ExportWorkItemQuery(workItem.Id, format), CancellationToken.None);

        Assert.Equal(contentType, result.ContentType);
        Assert.Equal($"ORB-1{extension}", result.FileName);
        Assert.Contains("Export this card", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public async Task Handle_Xlsx_ProducesReadableWorksheetWithExpectedFields()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Export this card", "Card description", WorkItemType.Task,
            Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var handler = new ExportWorkItemHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem), new WorkItemStatusRepositoryStub());

        var result = await handler.Handle(
            new ExportWorkItemQuery(workItem.Id, WorkItemExportFormat.Xlsx), CancellationToken.None);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        Assert.Equal("ORB-1.xlsx", result.FileName);

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        Assert.Equal("Key", worksheet.Cell(1, 1).GetString());
        Assert.Equal("Summary", worksheet.Cell(1, 5).GetString());
        Assert.Equal("Description", worksheet.Cell(1, 6).GetString());
        Assert.Equal("ORB-1", worksheet.Cell(2, 1).GetString());
        Assert.Equal("Task", worksheet.Cell(2, 2).GetString());
        Assert.Equal("Export this card", worksheet.Cell(2, 5).GetString());
        Assert.Equal("Card description", worksheet.Cell(2, 6).GetString());
    }

    [Fact]
    public async Task Handle_Docx_ProducesReadableDocumentWithExpectedFields()
    {
        var tenantId = Guid.NewGuid();
        var workItem = WorkItem.Create(
            tenantId, Guid.NewGuid(), 1, "ORB", "Export this card", "Card description", WorkItemType.Task,
            Priority.Medium, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var handler = new ExportWorkItemHandler(
            new TenantContextStub(tenantId), new WorkItemRepositoryStub(workItem), new WorkItemStatusRepositoryStub());

        var result = await handler.Handle(
            new ExportWorkItemQuery(workItem.Id, WorkItemExportFormat.Docx), CancellationToken.None);

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
        Assert.Equal("ORB-1.docx", result.FileName);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var text = string.Concat(body.Descendants<Text>().Select(t => t.Text));

        Assert.Contains("ORB-1: Export this card", text);
        Assert.Contains("Task", text);
        Assert.Contains("Card description", text);
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

    private sealed class WorkItemStatusRepositoryStub : IWorkItemStatusRepository
    {
        public Task AddAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddRangeAsync(IReadOnlyCollection<WorkItemStatusDefinition> definitions, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<WorkItemStatusDefinition?> GetAsync(
            Guid tenantId, Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItemStatusDefinition?>(null);

        public Task<IReadOnlyList<WorkItemStatusDefinition>> ListByProjectAsync(
            Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItemStatusDefinition>>([]);

        public Task<WorkItemStatusDefinition?> GetDefaultAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItemStatusDefinition?>(null);

        public Task<bool> IsInUseAsync(Guid tenantId, Guid projectId, Guid statusId, string statusKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(WorkItemStatusDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

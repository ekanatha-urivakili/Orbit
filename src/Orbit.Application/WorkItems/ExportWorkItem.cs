using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Access;

namespace Orbit.Application.WorkItems;

public enum WorkItemExportFormat
{
    Csv,
    Xml,
    Json,
}

public sealed record WorkItemExportResult(string FileName, string ContentType, byte[] Content);

public sealed record ExportWorkItemQuery(Guid WorkItemId, WorkItemExportFormat Format) : IQuery<WorkItemExportResult>;

public sealed class ExportWorkItemValidator : AbstractValidator<ExportWorkItemQuery>
{
    public ExportWorkItemValidator()
    {
        RuleFor(query => query.WorkItemId).NotEmpty();
        RuleFor(query => query.Format).IsInEnum();
    }
}

public sealed class ExportWorkItemHandler(
    ITenantContext tenantContext,
    IWorkItemRepository workItems) : IRequestHandler<ExportWorkItemQuery, WorkItemExportResult>
{
    public async Task<WorkItemExportResult> Handle(ExportWorkItemQuery request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var dto = WorkItemDto.From(workItem);

        return request.Format switch
        {
            WorkItemExportFormat.Csv => new WorkItemExportResult(
                $"{dto.Key}.csv", "text/csv", Encoding.UTF8.GetBytes(BuildCsv(dto))),
            WorkItemExportFormat.Xml => new WorkItemExportResult(
                $"{dto.Key}.xml", "application/xml", Encoding.UTF8.GetBytes(BuildXml(dto).ToString())),
            WorkItemExportFormat.Json => new WorkItemExportResult(
                $"{dto.Key}.json", "application/json",
                JsonSerializer.SerializeToUtf8Bytes(dto, new JsonSerializerOptions { WriteIndented = true })),
            _ => throw new ValidationException("Unsupported export format."),
        };
    }

    private static string BuildCsv(WorkItemDto dto)
    {
        string[] headers = ["Key", "Type", "Status", "Priority", "Summary", "Description", "Labels", "CreatedAt", "UpdatedAt"];
        string[] values =
        [
            dto.Key,
            dto.Type.ToString(),
            dto.Status.ToString(),
            dto.Priority.ToString(),
            EscapeCsv(dto.Summary),
            EscapeCsv(dto.Description ?? string.Empty),
            EscapeCsv(string.Join("; ", dto.Labels)),
            dto.CreatedAt.ToString("O"),
            dto.UpdatedAt.ToString("O"),
        ];
        return string.Join(',', headers) + "\r\n" + string.Join(',', values) + "\r\n";
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static XDocument BuildXml(WorkItemDto dto) =>
        new(
            new XElement(
                "WorkItem",
                new XElement("Key", dto.Key),
                new XElement("Type", dto.Type.ToString()),
                new XElement("Status", dto.Status.ToString()),
                new XElement("Priority", dto.Priority.ToString()),
                new XElement("Summary", dto.Summary),
                new XElement("Description", dto.Description ?? string.Empty),
                new XElement("Labels", dto.Labels.Select(label => new XElement("Label", label))),
                new XElement("CreatedAt", dto.CreatedAt.ToString("O")),
                new XElement("UpdatedAt", dto.UpdatedAt.ToString("O"))));
}

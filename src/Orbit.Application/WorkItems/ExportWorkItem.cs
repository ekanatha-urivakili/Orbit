using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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
    Xlsx,
    Docx,
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
    IWorkItemRepository workItems,
    IWorkItemStatusRepository workItemStatuses) : IRequestHandler<ExportWorkItemQuery, WorkItemExportResult>
{
    public async Task<WorkItemExportResult> Handle(ExportWorkItemQuery request, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetAsync(
            tenantContext.TenantId, request.WorkItemId, ProjectPermission.View, cancellationToken)
            ?? throw new NotFoundException("Work item was not found.");

        var dto = WorkItemDto.From(workItem);
        var statusName = (await workItemStatuses.GetAsync(
                tenantContext.TenantId, workItem.ProjectId, workItem.StatusId, cancellationToken))
            ?.Name ?? "Unknown";

        return request.Format switch
        {
            WorkItemExportFormat.Csv => new WorkItemExportResult(
                $"{dto.Key}.csv", "text/csv", Encoding.UTF8.GetBytes(BuildCsv(dto, statusName))),
            WorkItemExportFormat.Xml => new WorkItemExportResult(
                $"{dto.Key}.xml", "application/xml", Encoding.UTF8.GetBytes(BuildXml(dto, statusName).ToString())),
            WorkItemExportFormat.Json => new WorkItemExportResult(
                $"{dto.Key}.json", "application/json",
                JsonSerializer.SerializeToUtf8Bytes(dto, new JsonSerializerOptions { WriteIndented = true })),
            WorkItemExportFormat.Xlsx => new WorkItemExportResult(
                $"{dto.Key}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildXlsx(dto, statusName)),
            WorkItemExportFormat.Docx => new WorkItemExportResult(
                $"{dto.Key}.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                BuildDocx(dto, statusName)),
            _ => throw new ValidationException("Unsupported export format."),
        };
    }

    private static string BuildCsv(WorkItemDto dto, string statusName)
    {
        string[] headers = ["Key", "Type", "Status", "Priority", "Summary", "Description", "Labels", "CreatedAt", "UpdatedAt"];
        string[] values =
        [
            dto.Key,
            dto.Type.ToString(),
            statusName,
            dto.Priority.ToString(),
            EscapeCsv(dto.Summary),
            EscapeCsv(dto.Description ?? string.Empty),
            EscapeCsv(string.Join("; ", dto.Labels)),
            dto.CreatedAt.ToString("O"),
            dto.UpdatedAt.ToString("O"),
        ];
        return string.Join(',', headers) + "\r\n" + string.Join(',', values) + "\r\n";
    }

    private static string EscapeCsv(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
        {
            value = $"'{value}";
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static XDocument BuildXml(WorkItemDto dto, string statusName) =>
        new(
            new XElement(
                "WorkItem",
                new XElement("Key", dto.Key),
                new XElement("Type", dto.Type.ToString()),
                new XElement("Status", statusName),
                new XElement("Priority", dto.Priority.ToString()),
                new XElement("Summary", dto.Summary),
                new XElement("Description", dto.Description ?? string.Empty),
                new XElement("Labels", dto.Labels.Select(label => new XElement("Label", label))),
                new XElement("CreatedAt", dto.CreatedAt.ToString("O")),
                new XElement("UpdatedAt", dto.UpdatedAt.ToString("O"))));

    private static byte[] BuildXlsx(WorkItemDto dto, string statusName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Work Item");

        string[] headers = ["Key", "Type", "Status", "Priority", "Summary", "Description", "Labels", "CreatedAt", "UpdatedAt"];
        for (var column = 0; column < headers.Length; column++)
        {
            var cell = worksheet.Cell(1, column + 1);
            cell.Value = headers[column];
            cell.Style.Font.Bold = true;
        }

        worksheet.Cell(2, 1).Value = dto.Key;
        worksheet.Cell(2, 2).Value = dto.Type.ToString();
        worksheet.Cell(2, 3).Value = statusName;
        worksheet.Cell(2, 4).Value = dto.Priority.ToString();
        worksheet.Cell(2, 5).Value = dto.Summary;
        worksheet.Cell(2, 6).Value = dto.Description ?? string.Empty;
        worksheet.Cell(2, 7).Value = string.Join("; ", dto.Labels);
        worksheet.Cell(2, 8).Value = dto.CreatedAt.ToString("O");
        worksheet.Cell(2, 9).Value = dto.UpdatedAt.ToString("O");

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildDocx(WorkItemDto dto, string statusName)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            body.AppendChild(new Paragraph(new Run(new Text($"{dto.Key}: {dto.Summary}")
            {
                Space = SpaceProcessingModeValues.Preserve,
            }))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId { Val = "Heading1" }),
            });

            (string Label, string Value)[] fields =
            [
                ("Type", dto.Type.ToString()),
                ("Status", statusName),
                ("Priority", dto.Priority.ToString()),
                ("Labels", string.Join(", ", dto.Labels)),
                ("Created At", dto.CreatedAt.ToString("O")),
                ("Updated At", dto.UpdatedAt.ToString("O")),
            ];

            foreach (var (label, value) in fields)
            {
                body.AppendChild(new Paragraph(new Run(new Text($"{label}: {value}")
                {
                    Space = SpaceProcessingModeValues.Preserve,
                })));
            }

            body.AppendChild(new Paragraph(new Run(new Text("Description")
            {
                Space = SpaceProcessingModeValues.Preserve,
            }))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId { Val = "Heading2" }),
            });
            body.AppendChild(new Paragraph(new Run(new Text(dto.Description ?? string.Empty)
            {
                Space = SpaceProcessingModeValues.Preserve,
            })));

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }
}

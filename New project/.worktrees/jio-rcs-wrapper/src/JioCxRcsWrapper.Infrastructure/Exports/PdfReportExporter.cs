using JioCxRcsWrapper.Application.Common;
using JioCxRcsWrapper.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JioCxRcsWrapper.Infrastructure.Exports;

public sealed class PdfReportExporter : IPdfReportExporter
{
    public byte[] Export(IReadOnlyList<ContactReportRow> rows)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.Size(PageSizes.A4);
                page.Header().Text("JioCX Campaign Report").SemiBold().FontSize(18);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Campaign").SemiBold();
                        header.Cell().Text("Mobile").SemiBold();
                        header.Cell().Text("Status").SemiBold();
                        header.Cell().Text("Opened").SemiBold();
                        header.Cell().Text("Clicked").SemiBold();
                        header.Cell().Text("Error").SemiBold();
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Text(row.Campaign);
                        table.Cell().Text(row.MobileNumber);
                        table.Cell().Text(StatusLabels.For(row.Status));
                        table.Cell().Text(row.Opened ? "Yes" : "No");
                        table.Cell().Text(row.Clicked ? "Yes" : "No");
                        table.Cell().Text(row.ErrorMessage ?? row.LastError ?? "-");
                    }
                });
            });
        }).GeneratePdf();
    }
}

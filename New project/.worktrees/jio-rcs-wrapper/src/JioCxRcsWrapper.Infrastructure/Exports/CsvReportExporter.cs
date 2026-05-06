using System.Globalization;
using System.Text;
using CsvHelper;
using JioCxRcsWrapper.Application.Common;
using JioCxRcsWrapper.Application.Reports;

namespace JioCxRcsWrapper.Infrastructure.Exports;

public sealed class CsvReportExporter : ICsvReportExporter
{
    public string Export(IReadOnlyList<ContactReportRow> rows, bool includeDeveloperDiagnostics = false)
    {
        using var writer = new StringWriter(new StringBuilder(), CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteField("Campaign");
        csv.WriteField("MobileNumber");
        csv.WriteField("Status");
        csv.WriteField("Opened");
        csv.WriteField("Clicked");
        csv.WriteField("LastUpdated");
        if (includeDeveloperDiagnostics)
        {
            csv.WriteField("LastError");
            csv.WriteField("ErrorMessage");
            csv.WriteField("RequestHeaders");
            csv.WriteField("RequestPayload");
            csv.WriteField("ResponseStatusCode");
            csv.WriteField("ResponseBody");
        }
        csv.NextRecord();

        foreach (var row in rows)
        {
            csv.WriteField(row.Campaign);
            csv.WriteField(row.MobileNumber);
            csv.WriteField(StatusLabels.For(row.Status));
            csv.WriteField(row.Opened);
            csv.WriteField(row.Clicked);
            csv.WriteField(row.LastUpdated);
            if (includeDeveloperDiagnostics)
            {
                csv.WriteField(row.LastError);
                csv.WriteField(row.ErrorMessage);
                csv.WriteField(row.RequestHeaders);
                csv.WriteField(row.RequestPayload);
                csv.WriteField(row.ResponseStatusCode);
                csv.WriteField(row.ResponseBody);
            }
            csv.NextRecord();
        }

        return writer.ToString();
    }
}

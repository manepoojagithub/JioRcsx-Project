using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Reports;

public sealed record CampaignReportSummary(int CampaignId, string CampaignName, int ClientId, string ClientName, int TotalSent, int Delivered, int Failed);

public sealed record ContactReportRow(
    string Campaign,
    string MobileNumber,
    ContactStatus Status,
    bool Opened,
    bool Clicked,
    string? LastError,
    DateTimeOffset? LastUpdated,
    string? ErrorMessage = null,
    string? RequestHeaders = null,
    string? RequestPayload = null,
    string? ResponseStatusCode = null,
    string? ResponseBody = null);

public sealed record ContactReportResult(bool IsSuccess, IReadOnlyList<ContactReportRow> Rows, IReadOnlyList<string> Errors)
{
    public static ContactReportResult Success(IReadOnlyList<ContactReportRow> rows) => new(true, rows, []);

    public static ContactReportResult Failed(params string[] errors) => new(false, [], errors);
}

public sealed record ReportFilter(string? CampaignName = null, string? ClientName = null);

public sealed record ContactReportFilter(string? MobileNumber = null, JioCxRcsWrapper.Domain.Enums.ContactStatus? Status = null);

public interface IReportService
{
    Task<IReadOnlyList<CampaignReportSummary>> GetCampaignReportsAsync(ReportFilter? filter = null, CancellationToken cancellationToken = default);

    Task<ContactReportResult> GetContactReportAsync(int campaignId, ContactReportFilter? filter = null, CancellationToken cancellationToken = default);
}


public interface ICsvReportExporter
{
    string Export(IReadOnlyList<ContactReportRow> rows, bool includeDeveloperDiagnostics = false);
}

public interface IPdfReportExporter
{
    byte[] Export(IReadOnlyList<ContactReportRow> rows);
}

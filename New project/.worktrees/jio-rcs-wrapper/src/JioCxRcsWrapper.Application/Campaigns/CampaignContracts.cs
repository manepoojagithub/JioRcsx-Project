using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Campaigns;

public sealed record CreateCampaignRequest(string Name, int ClientId, CampaignType Type, DateTimeOffset? ScheduledAt, int? TemplateId, IReadOnlyList<string> ManualPhoneNumbers, bool IsRCSEnabled = true);

public sealed record CampaignSummary(
    int Id,
    string Name,
    int ClientId,
    string ClientName,
    CampaignType Type,
    CampaignStatus Status,
    DateTimeOffset? ScheduledAt,
    int ContactCount,
    int CompletedCount,
    int FailedCount,
    bool IsDisabled = false)
{
    public string QueueStatus => ContactCount == 0
        ? Status.ToString()
        : $"{CompletedCount} Success, {FailedCount} Failed out of {ContactCount}";
}

public sealed record CampaignOperationResult(bool IsSuccess, int? Id, IReadOnlyList<string> Errors)
{
    public static CampaignOperationResult Success(int? id = null) => new(true, id, []);

    public static CampaignOperationResult Failed(params string[] errors) => new(false, null, errors);

    public static CampaignOperationResult Failed(IEnumerable<string> errors) => new(false, null, errors.ToArray());
}

public sealed record ParsedContactsResult(bool IsValid, IReadOnlyList<string> MobileNumbers, IReadOnlyList<string> Errors)
{
    public static ParsedContactsResult Success(IReadOnlyList<string> mobileNumbers) => new(true, mobileNumbers, []);

    public static ParsedContactsResult Failed(IReadOnlyList<string> errors) => new(false, [], errors);
}

public interface IContactCsvParser
{
    ParsedContactsResult Parse(string csv);
}

public sealed record CampaignFilter(string? Name = null, string? ClientName = null, CampaignType? Type = null, CampaignStatus? Status = null);

public interface ICampaignService
{
    Task<IReadOnlyList<CampaignSummary>> ListAsync(CampaignFilter? filter = null, CancellationToken cancellationToken = default);
...
    Task<CampaignOperationResult> CreateDraftAsync(CreateCampaignRequest request, CancellationToken cancellationToken);

    Task<CampaignOperationResult> UploadContactsAsync(int campaignId, string csv, CancellationToken cancellationToken);

    Task<CampaignOperationResult> QueueCampaignAsync(int campaignId, CancellationToken cancellationToken);

    Task<CampaignOperationResult> DisableCampaignAsync(int campaignId, CancellationToken cancellationToken);

    Task<CampaignOperationResult> DeleteCampaignAsync(int campaignId, CancellationToken cancellationToken);
}

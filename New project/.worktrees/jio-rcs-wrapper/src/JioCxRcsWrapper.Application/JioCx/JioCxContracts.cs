namespace JioCxRcsWrapper.Application.JioCx;

public interface IJioCxClient
{
    Task<JioCxUploadResult> UploadFileAsync(string apiKey, string agentId, Stream file, string fileName, string contentType, CancellationToken cancellationToken);
    Task<JioCxSendResult> SendMessageAsync(string apiKey, JioCxSendRequest request, CancellationToken cancellationToken);
    Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, IReadOnlyList<string> phoneNumbers, CancellationToken cancellationToken);
    Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, string phoneNumber, CancellationToken cancellationToken);
}

public sealed record JioCxUploadResult(bool Succeeded, int StatusCode, string ResponseJson, string? PublicUrl, string? RequestPayload = null);

public sealed record JioCxSendRequest(
    string MessageId,
    string AgentId,
    string? CampaignId,
    IReadOnlyList<string> Contacts,
    object Data,
    object? SmsFallback = null);

public sealed record JioCxSendResult(bool Succeeded, int StatusCode, string ResponseJson, string? RequestPayload = null);

public sealed record JioCxCapabilityResult(bool Succeeded, int StatusCode, string ResponseJson);

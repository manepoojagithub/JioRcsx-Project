namespace JioCxRcsWrapper.Application.Clients;

public sealed record CreateClientRequest(
    string BrandName,
    string AgentName,
    string AgentId,
    string ApiKey,
    string SiteName,
    string ManagerName,
    string ManagerEmail,
    string ManagerPassword,
    string? LogoPath,
    int Credits = 100,
    int CreditCostPerMessage = 1,
    int LowCreditThreshold = 10);

public sealed record ClientSummary(int Id, string BrandName, string AgentName, string AgentId, string SiteName, int Credits, int CreditCostPerMessage, int LowCreditThreshold);

public sealed record ClientDetails(int Id, string BrandName, string AgentName, string AgentId, string SiteName, string? LogoPath, int Credits, int CreditCostPerMessage, int LowCreditThreshold, string? ManagerEmail = null, bool WebhookAuditEnabled = false);

public sealed record UpdateClientRequest(
    int Id,
    string BrandName,
    string AgentName,
    string AgentId,
    string? ApiKey,
    string SiteName,
    string? LogoPath,
    int Credits = 100,
    int CreditCostPerMessage = 1,
    int LowCreditThreshold = 10,
    string? ManagerEmail = null,
    bool WebhookAuditEnabled = false);

public sealed record BrandingResult(string SiteName, string? LogoPath);

public interface ISecretProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}
public sealed record ClientFilter(string? BrandName = null, string? AgentName = null, string? AgentId = null, string? SiteName = null);

public interface IClientOnboardingService
{
    Task<int> CreateAsync(CreateClientRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientSummary>> ListAsync(ClientFilter? filter = null, CancellationToken cancellationToken = default);

    Task<ClientDetails?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateClientRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IBrandingService
{
    Task<BrandingResult> GetBrandingAsync(int? clientId, CancellationToken cancellationToken = default);
}

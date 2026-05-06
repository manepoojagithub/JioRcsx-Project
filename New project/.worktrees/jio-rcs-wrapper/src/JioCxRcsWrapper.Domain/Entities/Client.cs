using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class Client : BaseEntity
{
    public string BrandName { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int CreditCostPerMessage { get; set; } = 1;
    public int LowCreditThreshold { get; set; } = 10;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

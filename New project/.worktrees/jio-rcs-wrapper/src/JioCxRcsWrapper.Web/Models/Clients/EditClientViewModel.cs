using System.ComponentModel.DataAnnotations;

namespace JioCxRcsWrapper.Web.Models.Clients;

public sealed class EditClientViewModel
{
    public int Id { get; set; }

    [Required]
    public string BrandName { get; set; } = string.Empty;

    [Required]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    public string AgentId { get; set; } = string.Empty;

    public string? AgentKey { get; set; }

    public string? LogoPath { get; set; }

    public IFormFile? LogoFile { get; set; }

    [Required]
    public string SiteName { get; set; } = string.Empty;
    [Range(0, int.MaxValue)]
    public int Credits { get; set; }
    [Range(1, int.MaxValue)]
    public int CreditCostPerMessage { get; set; } = 1;
    [Range(0, int.MaxValue)]
    public int LowCreditThreshold { get; set; } = 10;

    [EmailAddress]
    public string? ManagerEmail { get; set; }

    public string? ManagerPassword { get; set; }

    public string AgentUseCase { get; set; } = "Transactional";

    public bool WebhookAuditEnabled { get; set; }
}

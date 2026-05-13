using System.ComponentModel.DataAnnotations;

namespace JioCxRcsWrapper.Web.Models.Clients;

public sealed class CreateClientViewModel
{
    [Required]
    public string BrandName { get; set; } = string.Empty;
    [Required]
    public string AgentName { get; set; } = string.Empty;
    [Required]
    public string AgentId { get; set; } = string.Empty;
    [Required]
    public string AgentKey { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public IFormFile? LogoFile { get; set; }
    [Required]
    public string SiteName { get; set; } = string.Empty;
    [Required]
    public string AgentUseCase { get; set; } = "Transactional";
    [Required]
    public string ManagerName { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string ManagerEmail { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string ManagerPassword { get; set; } = string.Empty;
    [Range(0, int.MaxValue)]
    public int Credits { get; set; } = 100;
    [Range(1, int.MaxValue)]
    public int CreditCostPerMessage { get; set; } = 1;
    [Range(0, int.MaxValue)]
    public int LowCreditThreshold { get; set; } = 10;
}

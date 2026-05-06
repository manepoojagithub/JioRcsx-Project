using System.ComponentModel.DataAnnotations;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Web.Models.Campaigns;

public sealed class CreateCampaignViewModel
{
    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int ClientId { get; set; }

    public CampaignType Type { get; set; } = CampaignType.Schedule;

    public DateTimeOffset? ScheduledAt { get; set; }

    [Display(Name = "IsRCSEnabled")]
    public bool IsRCSEnabled { get; set; } = true;

    public int? TemplateId { get; set; }

    public string? PhoneNumbers { get; set; }
}

public sealed class UploadContactsViewModel
{
    [Required]
    public int CampaignId { get; set; }

    [Required]
    public IFormFile? CsvFile { get; set; }
}

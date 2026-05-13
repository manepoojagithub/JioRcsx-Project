using System.ComponentModel.DataAnnotations;

namespace JioCxRcsWrapper.Web.Models.ApiSettings;

public sealed class ApiSettingsViewModel
{
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string UploadFilePath { get; set; } = string.Empty;

    [Required]
    public string SendMessagePath { get; set; } = string.Empty;

    [Required]
    public string CheckCapabilityPath { get; set; } = string.Empty;
}

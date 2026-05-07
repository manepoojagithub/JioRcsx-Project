using System.ComponentModel.DataAnnotations;

namespace JioCxRcsWrapper.Web.Models.Auth;

public sealed class ProfileViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
    public string? ConfirmPassword { get; set; }
}

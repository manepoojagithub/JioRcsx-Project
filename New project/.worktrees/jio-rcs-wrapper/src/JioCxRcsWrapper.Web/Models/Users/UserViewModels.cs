using System.ComponentModel.DataAnnotations;

namespace JioCxRcsWrapper.Web.Models.Users;

public sealed class CreateUserViewModel
{
    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public int RoleId { get; set; }

    public int? ClientId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeveloper { get; set; }
}

public sealed class EditUserViewModel
{
    [Required]
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    public int RoleId { get; set; }

    public int? ClientId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeveloper { get; set; }
}

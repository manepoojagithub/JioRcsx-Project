using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class ClientBrandingSetting : BaseEntity
{
    public int? ClientId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public bool IsDefault { get; set; }
}

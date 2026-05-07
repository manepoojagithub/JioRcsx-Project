using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class UserCreditHistory : BaseEntity
{
    public int UserId { get; set; }
    public int Amount { get; set; }
    public int PreviousBalance { get; set; }
    public int NewBalance { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

using JioCxRcsWrapper.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace JioCxRcsWrapper.Web.Models.MessageBuilder;

public sealed class MessageBuilderViewModel
{
    public int? Id { get; set; }

    public string MessageType { get; set; } = "PlainText";

    [Required]
    public string? TemplateName { get; set; }

    public int? ClientId { get; set; }

    // For Plain Text
    public string? Text { get; set; }

    // For Standalone Card & Carousel
    public List<MessageCardViewModel> Cards { get; set; } = [new()];
}

public sealed class MessageCardViewModel
{
    public IFormFile? MediaFile { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? MediaUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? LocalMediaPath { get; set; }

    public string? MediaContentType { get; set; }

    public List<CtaViewModel> Ctas { get; set; } = [];
}

public sealed class CtaViewModel
{
    [Required]
    public string Text { get; set; } = string.Empty;

    public CtaActionType ActionType { get; set; } = CtaActionType.OpenUrl;

    [Required]
    public string Value { get; set; } = string.Empty;

    public string PostBackData { get; set; } = string.Empty;
}

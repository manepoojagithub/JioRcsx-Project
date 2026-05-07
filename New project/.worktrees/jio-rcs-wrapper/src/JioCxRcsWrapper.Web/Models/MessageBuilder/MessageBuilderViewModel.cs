using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Web.Models.MessageBuilder;

public sealed class MessageBuilderViewModel
{
    public int? Id { get; set; }

    public string MessageType { get; set; } = "PlainText";

    public string? TemplateName { get; set; }

    public int? ClientId { get; set; }

    public IFormFile? MediaFile { get; set; }

    public string? Text { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Footer { get; set; }

    public string? MediaUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? LocalMediaPath { get; set; }

    public string? MediaContentType { get; set; }

    public List<CtaViewModel> Ctas { get; set; } = [];
}

public sealed class CtaViewModel
{
    public string Text { get; set; } = string.Empty;

    public CtaActionType ActionType { get; set; } = CtaActionType.OpenUrl;

    public string Value { get; set; } = string.Empty;

    public string PostBackData { get; set; } = string.Empty;
}

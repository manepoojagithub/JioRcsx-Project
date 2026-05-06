using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Messages;

public sealed record PlainTextMessageDraft(string Text, IReadOnlyList<CtaDraft>? Suggestions = null);

public sealed record RichCardDraft(
    string? Title,
    string? Description,
    string? MediaUrl,
    string? ThumbnailUrl,
    IReadOnlyList<CtaDraft> Ctas);

public sealed record CtaDraft(string Text, CtaActionType ActionType, string Value, string PostBackData);

public sealed record CarouselDraft(string CardWidth, IReadOnlyList<CarouselCardDraft> Cards);

public sealed record CarouselCardDraft(string Title, string Description, string MediaUrl, IReadOnlyList<CtaDraft> Ctas);

public sealed record MessagePayloadResult(bool IsValid, string? PayloadJson, IReadOnlyList<string> Errors)
{
    public static MessagePayloadResult Success(string payloadJson) => new(true, payloadJson, []);

    public static MessagePayloadResult Failed(IReadOnlyList<string> errors) => new(false, null, errors);
}

public interface IMessagePayloadService
{
    MessagePayloadResult BuildPlainText(PlainTextMessageDraft draft);

    MessagePayloadResult BuildRichCard(RichCardDraft draft);

    MessagePayloadResult BuildCarousel(CarouselDraft draft);
}

using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Messages;

public sealed record CtaDraft(string Text, CtaActionType ActionType, string Value, string PostBackData);

public sealed record StandaloneCardDraft(string? Title, string? Description, string? MediaUrl, string? ThumbnailUrl, IReadOnlyList<CtaDraft> Ctas);

public sealed record CarouselDraft(IReadOnlyList<StandaloneCardDraft> Cards);

public sealed record MessagePayloadResult(bool IsValid, string PayloadJson, IReadOnlyList<string> Errors)
{
    public static MessagePayloadResult Success(string json) => new(true, json, []);
    public static MessagePayloadResult Failed(params string[] errors) => new(false, string.Empty, errors);
    public static MessagePayloadResult Failed(IEnumerable<string> errors) => new(false, string.Empty, errors.ToArray());
}

public interface IMessagePayloadService
{
    MessagePayloadResult BuildPlainText(string text);
    MessagePayloadResult BuildStandaloneCard(StandaloneCardDraft draft);
    MessagePayloadResult BuildCarousel(CarouselDraft draft);
}

using System.Text.Json;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Messages;

public sealed class MessagePayloadBuilder : IMessagePayloadService
{
    public MessagePayloadResult BuildPlainText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return MessagePayloadResult.Failed("Text is required.");
        var payload = new { content = new { text } };
        return MessagePayloadResult.Success(JsonSerializer.Serialize(payload));
    }

    public MessagePayloadResult BuildStandaloneCard(StandaloneCardDraft draft)
    {
        var errors = ValidateCard(draft);
        if (errors.Count > 0) return MessagePayloadResult.Failed(errors);

        var payload = new
        {
            content = new
            {
                richCardDetails = new
                {
                    standalone = new
                    {
                        cardOrientation = "VERTICAL",
                        cardContent = MapCardContent(draft)
                    }
                }
            }
        };

        return MessagePayloadResult.Success(JsonSerializer.Serialize(payload));
    }

    public MessagePayloadResult BuildCarousel(CarouselDraft draft)
    {
        if (draft.Cards.Count < 2 || draft.Cards.Count > 10)
        {
            return MessagePayloadResult.Failed("Carousel must have between 2 and 10 cards.");
        }

        var errors = new List<string>();
        for (int i = 0; i < draft.Cards.Count; i++)
        {
            var cardErrors = ValidateCard(draft.Cards[i]);
            foreach (var err in cardErrors) errors.Add($"Card {i + 1}: {err}");
        }

        if (errors.Count > 0) return MessagePayloadResult.Failed(errors);

        var payload = new
        {
            content = new
            {
                richCardDetails = new
                {
                    carousel = new
                    {
                        cardWidth = "MEDIUM_WIDTH",
                        cardContents = draft.Cards.Select(MapCardContent).ToArray()
                    }
                }
            }
        };

        return MessagePayloadResult.Success(JsonSerializer.Serialize(payload));
    }

    private static List<string> ValidateCard(StandaloneCardDraft draft)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Title) && string.IsNullOrWhiteSpace(draft.Description) && string.IsNullOrWhiteSpace(draft.MediaUrl))
        {
            errors.Add("Card must have at least a Title, Description, or Media.");
        }

        if (draft.Title?.Length > 200) errors.Add("Title cannot exceed 200 characters.");
        if (draft.Description?.Length > 2000) errors.Add("Description cannot exceed 2000 characters.");
        if (draft.Ctas.Count > 4) errors.Add("Maximum 4 buttons allowed per card.");

        return errors;
    }

    private static object MapCardContent(StandaloneCardDraft draft)
    {
        return new
        {
            media = string.IsNullOrWhiteSpace(draft.MediaUrl) ? null : new
            {
                contentUrl = draft.MediaUrl,
                thumbnailUrl = draft.ThumbnailUrl,
                height = "MEDIUM"
            },
            title = draft.Title,
            description = draft.Description,
            suggestions = draft.Ctas.Select(cta => new
            {
                action = MapAction(cta)
            }).ToArray()
        };
    }

    private static object MapAction(CtaDraft cta)
    {
        return cta.ActionType switch
        {
            CtaActionType.OpenUrl => new
            {
                displayText = cta.Text,
                postbackData = cta.PostBackData,
                openUrlAction = new { url = cta.Value }
            },
            CtaActionType.Dialer => new
            {
                displayText = cta.Text,
                postbackData = cta.PostBackData,
                dialerAction = new { phoneNumber = cta.Value }
            },
            _ => new { displayText = cta.Text, postbackData = cta.PostBackData }
        };
    }
}

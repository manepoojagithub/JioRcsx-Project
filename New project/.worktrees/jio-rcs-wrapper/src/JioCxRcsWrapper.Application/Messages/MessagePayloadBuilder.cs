using System.Text.Json;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Messages;

public sealed class MessagePayloadService : IMessagePayloadService
{
    public MessagePayloadResult BuildPlainText(PlainTextMessageDraft draft) => MessagePayloadBuilder.BuildPlainText(draft);

    public MessagePayloadResult BuildRichCard(RichCardDraft draft) => MessagePayloadBuilder.BuildRichCard(draft);

    public MessagePayloadResult BuildCarousel(CarouselDraft draft) => MessagePayloadBuilder.BuildCarousel(draft);
}

public static class MessagePayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MessagePayloadResult BuildPlainText(PlainTextMessageDraft draft)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Text))
        {
            errors.Add("Plain text message is required.");
        }

        if (errors.Count > 0)
        {
            return MessagePayloadResult.Failed(errors);
        }

        var content = new Dictionary<string, object>
        {
            ["plainText"] = draft.Text.Trim()
        };

        var suggestions = BuildSuggestions(draft.Suggestions ?? []);
        if (suggestions.Errors.Count > 0)
        {
            return MessagePayloadResult.Failed(suggestions.Errors);
        }

        if (suggestions.Values.Count > 0)
        {
            content["suggestions"] = suggestions.Values;
        }

        var payload = new Dictionary<string, object> { ["content"] = content };

        return MessagePayloadResult.Success(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static MessagePayloadResult BuildRichCard(RichCardDraft draft)
    {
        var errors = ValidateRichCard(draft);
        if (errors.Count > 0)
        {
            return MessagePayloadResult.Failed(errors);
        }

        var suggestions = BuildSuggestions(draft.Ctas);
        if (suggestions.Errors.Count > 0)
        {
            return MessagePayloadResult.Failed(suggestions.Errors);
        }

        var payload = new
        {
            content = new
            {
                richCardDetails = new
                {
                    standalone = new
                    {
                        cardOrientation = "VERTICAL",
                        content = new
                        {
                            cardTitle = draft.Title!.Trim(),
                            cardDescription = draft.Description!.Trim(),
                            cardFooter = draft.Footer?.Trim(),
                            cardMedia = new
                            {
                                mediaHeight = "MEDIUM",
                                contentInfo = new
                                {
                                    fileUrl = draft.MediaUrl!.Trim()
                                }
                            },
                            suggestions = suggestions.Values
                        }
                    }
                }
            }
        };

        return MessagePayloadResult.Success(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static MessagePayloadResult BuildCarousel(CarouselDraft draft)
    {
        var errors = new List<string>();
        if (draft.Cards.Count == 0)
        {
            errors.Add("At least one carousel card is required.");
        }

        var contents = new List<object>();
        foreach (var card in draft.Cards)
        {
            errors.AddRange(ValidateCardFields(card.Title, card.Description, card.MediaUrl));
            var suggestions = BuildSuggestions(card.Ctas);
            errors.AddRange(suggestions.Errors);
            contents.Add(new
            {
                cardTitle = card.Title.Trim(),
                cardDescription = card.Description.Trim(),
                cardFooter = card.Footer?.Trim(),
                cardMedia = new
                {
                    mediaHeight = "MEDIUM",
                    contentInfo = new
                    {
                        fileUrl = card.MediaUrl.Trim()
                    }
                },
                suggestions = suggestions.Values
            });
        }

        if (errors.Count > 0)
        {
            return MessagePayloadResult.Failed(errors);
        }

        var payload = new
        {
            content = new
            {
                richCardDetails = new
                {
                    carousel = new
                    {
                        cardWidth = string.IsNullOrWhiteSpace(draft.CardWidth) ? "MEDIUM_WIDTH" : draft.CardWidth.Trim(),
                        contents
                    }
                }
            }
        };

        return MessagePayloadResult.Success(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static List<string> ValidateRichCard(RichCardDraft draft)
    {
        var errors = new List<string>();

        errors.AddRange(ValidateCardFields(draft.Title, draft.Description, draft.MediaUrl));

        if (draft.Ctas.Count > 4)
        {
            errors.Add("Maximum 4 CTAs are allowed.");
        }

        return errors;
    }

    private static List<string> ValidateCardFields(string? title, string? description, string? mediaUrl)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("Title is required.");
        }
        else if (title.Length > 80)
        {
            errors.Add("Title must be 80 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors.Add("Description is required.");
        }
        else if (description.Length > 2000)
        {
            errors.Add("Description must be 2000 characters or fewer.");
        }

        if (!IsHttpsUrl(mediaUrl))
        {
            errors.Add("Media URL must use HTTPS.");
        }

        return errors;
    }

    private static SuggestionBuildResult BuildSuggestions(IReadOnlyList<CtaDraft> ctas)
    {
        var errors = new List<string>();
        var suggestions = new List<object>();

        if (ctas.Count > 4)
        {
            errors.Add("Maximum 4 CTAs are allowed.");
        }

        foreach (var cta in ctas)
        {
            if (string.IsNullOrWhiteSpace(cta.Text))
            {
                errors.Add("CTA text is required.");
            }

            if (errors.Count > 0)
            {
                continue;
            }

            var postBackData = string.IsNullOrWhiteSpace(cta.PostBackData) ? cta.Text.Trim() : cta.PostBackData.Trim();
            var postBack = new Dictionary<string, object> { ["data"] = postBackData };
            if (cta.ActionType == CtaActionType.SuggestedReply)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    ["reply"] = new Dictionary<string, object>
                    {
                        ["plainText"] = cta.Text.Trim(),
                        ["postBack"] = postBack
                    }
                });
                continue;
            }

            var action = new Dictionary<string, object>
            {
                ["plainText"] = cta.Text.Trim(),
                ["postBack"] = postBack
            };

            switch (cta.ActionType)
            {
                case CtaActionType.OpenUrl:
                    if (!IsHttpsUrl(cta.Value))
                    {
                        errors.Add("CTA URLs must use HTTPS.");
                        continue;
                    }

                    action["openUrl"] = new Dictionary<string, object> { ["url"] = cta.Value.Trim() };
                    break;
                case CtaActionType.Dialer:
                    action["dialerAction"] = new Dictionary<string, object> { ["phoneNumber"] = cta.Value.Trim() };
                    break;
                case CtaActionType.Calendar:
                    var calendar = TryReadCalendar(cta.Value);
                    if (calendar is null)
                    {
                        errors.Add("Calendar CTA value must be JSON with startTime, endTime, title, and description.");
                        continue;
                    }

                    action["createCalendarEvent"] = calendar;
                    break;
                case CtaActionType.Location:
                    var location = TryReadLocation(cta.Value);
                    if (location is null)
                    {
                        errors.Add("Location CTA value must be JSON with latitude, longitude, and label.");
                        continue;
                    }

                    action["showLocation"] = location;
                    break;
                default:
                    errors.Add("Invalid CTA action type.");
                    continue;
            }

            suggestions.Add(new Dictionary<string, object> { ["action"] = action });
        }

        return new SuggestionBuildResult(suggestions, errors);
    }

    private static Dictionary<string, object>? TryReadCalendar(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new Dictionary<string, object>
            {
                ["startTime"] = root.GetProperty("startTime").GetString() ?? string.Empty,
                ["endTime"] = root.GetProperty("endTime").GetString() ?? string.Empty,
                ["title"] = root.GetProperty("title").GetString() ?? string.Empty,
                ["description"] = root.GetProperty("description").GetString() ?? string.Empty
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static Dictionary<string, object>? TryReadLocation(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new Dictionary<string, object>
            {
                ["coordinAtes"] = new Dictionary<string, object>
                {
                    ["latitude"] = root.GetProperty("latitude").GetDecimal(),
                    ["longitude"] = root.GetProperty("longitude").GetDecimal()
                },
                ["label"] = root.GetProperty("label").GetString() ?? string.Empty
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static bool IsHttpsUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    private sealed record SuggestionBuildResult(IReadOnlyList<object> Values, IReadOnlyList<string> Errors);
}

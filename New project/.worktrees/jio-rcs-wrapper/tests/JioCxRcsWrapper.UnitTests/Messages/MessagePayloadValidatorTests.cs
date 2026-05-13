using System.Text.Json;
using FluentAssertions;
using JioCxRcsWrapper.Application.Messages;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Messages;

public sealed class MessagePayloadValidatorTests
{
    [Fact]
    public void PlainText_WithText_IsValidAndBuildsDocumentedPayload()
    {
        var result = MessagePayloadBuilder.BuildPlainText(new PlainTextMessageDraft("This is the Sample for Plain Text"));

        result.IsValid.Should().BeTrue();
        using var document = JsonDocument.Parse(result.PayloadJson!);
        document.RootElement.GetProperty("content").GetProperty("plainText").GetString()
            .Should().Be("This is the Sample for Plain Text");
    }

    [Fact]
    public void StandaloneCard_WithOpenUrl_IsValidAndBuildsDocumentedPayload()
    {
        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            "Card Title goes here",
            "Card Description goes here",
            null,
            "https://cdn.example.com/image.png",
            null,
            [new CtaDraft("Button 1", CtaActionType.OpenUrl, "https://example.com", "call_back_data_for_button_1_goes_here")]));

        result.IsValid.Should().BeTrue();
        using var document = JsonDocument.Parse(result.PayloadJson!);
        var action = document.RootElement
            .GetProperty("content")
            .GetProperty("richCardDetails")
            .GetProperty("standalone")
            .GetProperty("content")
            .GetProperty("suggestions")[0]
            .GetProperty("action");

        action.GetProperty("plainText").GetString().Should().Be("Button 1");
        action.GetProperty("postBack").GetProperty("data").GetString().Should().Be("call_back_data_for_button_1_goes_here");
        action.GetProperty("openUrl").GetProperty("url").GetString().Should().Be("https://example.com");
    }

    [Fact]
    public void StandaloneCard_WithFiveCtas_IsRejected()
    {
        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            "Title",
            "Description",
            null,
            "https://cdn.example.com/image.png",
            null,
            Enumerable.Range(1, 5)
                .Select(index => new CtaDraft($"Button {index}", CtaActionType.OpenUrl, "https://example.com", $"pb-{index}"))
                .ToArray()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Maximum 4 CTAs are allowed.");
    }

    [Fact]
    public void StandaloneCard_WithHttpUrl_IsRejected()
    {
        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            "Title",
            "Description",
            null,
            "https://cdn.example.com/image.png",
            null,
            [new CtaDraft("Button", CtaActionType.OpenUrl, "http://example.com", "pb")]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("CTA URLs must use HTTPS.");
    }

    [Fact]
    public void StandaloneCard_WithDialerAction_BuildsDocumentedPayload()
    {
        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            "Title",
            "Description",
            null,
            "https://cdn.example.com/image.png",
            null,
            [new CtaDraft("Call", CtaActionType.Dialer, "+918000000000", "pb")]));

        result.IsValid.Should().BeTrue();
        using var document = JsonDocument.Parse(result.PayloadJson!);
        var action = FirstStandaloneCardSuggestion(document).GetProperty("action");
        action.GetProperty("dialerAction").GetProperty("phoneNumber").GetString().Should().Be("+918000000000");
    }

    [Fact]
    public void StandaloneCard_WithCalendarAction_BuildsDocumentedPayload()
    {
        var calendarJson = """
            {"startTime":"2023-06-26T15:01:23Z","endTime":"2023-06-26T18:01:23Z","title":"RCS Seminar","description":"Session 1 of 4"}
            """;

        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            "Title",
            "Description",
            null,
            "https://cdn.example.com/image.png",
            null,
            [new CtaDraft("Calendar", CtaActionType.Calendar, calendarJson, "pb")]));

        result.IsValid.Should().BeTrue();
        using var document = JsonDocument.Parse(result.PayloadJson!);
        var calendar = FirstStandaloneCardSuggestion(document).GetProperty("action").GetProperty("createCalendarEvent");
        calendar.GetProperty("startTime").GetString().Should().Be("2023-06-26T15:01:23Z");
        calendar.GetProperty("title").GetString().Should().Be("RCS Seminar");
    }

    [Fact]
    public void StandaloneCard_WithLocationAction_BuildsDocumentedPayload()
    {
        var locationJson = """{"latitude":21.5937,"longitude":78.9629,"label":"Label - Show Location"}""";

        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            "Title",
            "Description",
            null,
            "https://cdn.example.com/image.png",
            null,
            [new CtaDraft("Location", CtaActionType.Location, locationJson, "pb")]));

        result.IsValid.Should().BeTrue();
        using var document = JsonDocument.Parse(result.PayloadJson!);
        var location = FirstStandaloneCardSuggestion(document).GetProperty("action").GetProperty("showLocation");
        location.GetProperty("coordinAtes").GetProperty("latitude").GetDecimal().Should().Be(21.5937m);
        location.GetProperty("label").GetString().Should().Be("Label - Show Location");
    }

    [Fact]
    public void PlainText_WithSuggestedReply_BuildsDocumentedPayload()
    {
        var result = MessagePayloadBuilder.BuildPlainText(new PlainTextMessageDraft(
            "Your order has been delivered.",
            [new CtaDraft("Yes, Absolutely", CtaActionType.SuggestedReply, string.Empty, "suggestion_1")]));

        result.IsValid.Should().BeTrue();
        using var document = JsonDocument.Parse(result.PayloadJson!);
        var reply = document.RootElement.GetProperty("content").GetProperty("suggestions")[0].GetProperty("reply");
        reply.GetProperty("plainText").GetString().Should().Be("Yes, Absolutely");
        reply.GetProperty("postBack").GetProperty("data").GetString().Should().Be("suggestion_1");
    }

    [Fact]
    public void Carousel_WithOpenUrl_BuildsDocumentedPayload()
    {
        var result = MessagePayloadBuilder.BuildCarousel(new CarouselDraft(
            "MEDIUM_WIDTH",
            [
                new CarouselCardDraft(
                    "Card 1",
                    "Description 1",
                    null,
                    "https://cdn.example.com/1.png",
                    [new CtaDraft("Pay Now", CtaActionType.OpenUrl, "https://example.com/pay", "pb-1")]),
                new CarouselCardDraft(
                    "Card 2",
                    "Description 2",
                    null,
                    "https://cdn.example.com/2.png",
                    [])
            ]));

        result.IsValid.Should().BeTrue();
        using var document = JsonDocument.Parse(result.PayloadJson!);
        var carousel = document.RootElement.GetProperty("content").GetProperty("richCardDetails").GetProperty("carousel");
        carousel.GetProperty("cardWidth").GetString().Should().Be("MEDIUM_WIDTH");
        carousel.GetProperty("contents").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void StandaloneCard_TitleOver80_IsRejected()
    {
        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            new string('T', 81),
            "Description",
            null,
            "https://cdn.example.com/image.png",
            null,
            []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Title must be 80 characters or fewer.");
    }

    [Fact]
    public void StandaloneCard_DescriptionOver2000_IsRejected()
    {
        var result = MessagePayloadBuilder.BuildStandaloneCard(new StandaloneCardDraft(
            "Title",
            new string('D', 2001),
            null,
            "https://cdn.example.com/image.png",
            null,
            []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Description must be 2000 characters or fewer.");
    }

    private static JsonElement FirstStandaloneCardSuggestion(JsonDocument document)
    {
        return document.RootElement
            .GetProperty("content")
            .GetProperty("richCardDetails")
            .GetProperty("standalone")
            .GetProperty("content")
            .GetProperty("suggestions")[0];
    }
}

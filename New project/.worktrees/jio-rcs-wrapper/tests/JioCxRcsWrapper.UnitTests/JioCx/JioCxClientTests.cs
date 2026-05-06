using System.Net;
using System.Text.Json;
using FluentAssertions;
using JioCxRcsWrapper.Application.Common.Options;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Infrastructure.JioCx;
using Microsoft.Extensions.Options;

namespace JioCxRcsWrapper.UnitTests.JioCx;

public sealed class JioCxClientTests
{
    [Fact]
    public async Task UploadFile_SendsDocumentedMultipartRequest()
    {
        var handler = new CaptureHandler("""{"url":"https://cdn.example.com/file.png"}""");
        var client = CreateClient(handler);

        await client.UploadFileAsync("api-key", "agent-1", new MemoryStream([1, 2, 3]), "file.png", "image/png", CancellationToken.None);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.PathAndQuery.Should().Be("/api/v1/uploadFile");
        handler.Request.Headers.GetValues("x-apikey").Single().Should().Be("api-key");
        handler.Body.Should().Contain("agentId");
        handler.Body.Should().Contain("agent-1");
    }

    [Fact]
    public async Task UploadFile_ReadsNestedPublicUrlFromResponse()
    {
        var handler = new CaptureHandler("""{"data":{"fileUrl":"https://cdn.example.com/file.png"}}""");
        var client = CreateClient(handler);

        var result = await client.UploadFileAsync("api-key", "agent-1", new MemoryStream([1, 2, 3]), "file.png", "image/png", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.PublicUrl.Should().Be("https://cdn.example.com/file.png");
    }

    [Fact]
    public async Task UploadFile_ExposesStatusAndBodyWhenJioCxRejectsRequest()
    {
        var handler = new CaptureHandler("""{"message":"Invalid agent id"}""", HttpStatusCode.Unauthorized);
        var client = CreateClient(handler);

        var result = await client.UploadFileAsync("api-key", "bad-agent", new MemoryStream([1, 2, 3]), "file.png", "image/png", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.ResponseJson.Should().Contain("Invalid agent id");
    }

    [Fact]
    public async Task UploadFile_WhenStreamCopyFails_ReturnsFailedResult()
    {
        var handler = new ThrowingHandler(new HttpRequestException("Error while copying content to a stream."));
        var client = CreateClient(handler);

        var result = await client.UploadFileAsync("api-key", "agent-1", new MemoryStream([1, 2, 3]), "file.png", "image/png", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.StatusCode.Should().Be(0);
        result.ResponseJson.Should().Contain("Error while copying content to a stream");
        result.PublicUrl.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_SendsDocumentedJsonShape()
    {
        var handler = new CaptureHandler("""{"status":"ok"}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync("api-key", new JioCxSendRequest("message-1", "agent-1", "campaign-10", ["+918000000000"], new { content = new { plainText = "Hi" } }), CancellationToken.None);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.PathAndQuery.Should().Be("/api/v1/sendMessage");
        handler.Request.Headers.GetValues("x-apikey").Single().Should().Be("api-key");
        handler.Body.Should().NotBeNull();
        using var doc = JsonDocument.Parse(handler.Body!);
        doc.RootElement.GetProperty("messageID").GetString().Should().Be("message-1");
        doc.RootElement.GetProperty("agentID").GetString().Should().Be("agent-1");
        doc.RootElement.GetProperty("campaignID").GetString().Should().Be("campaign-10");
        doc.RootElement.GetProperty("contacts")[0].GetString().Should().Be("+918000000000");
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SendMessage_WithSmsFallback_SendsDocumentedDataSmsShape()
    {
        var handler = new CaptureHandler("""{"status":"ok"}""");
        var client = CreateClient(handler);

        var sms = new Dictionary<string, string>
        {
            ["sender_id"] = "JIDHGD",
            ["domain_id"] = "domain",
            ["sms_type"] = "T",
            ["sms_content_type"] = "Static",
            ["dlt_entity_id"] = "120854436650",
            ["body"] = "SMS Body",
            ["dlt_template_id"] = "1207177191037"
        };

        await client.SendMessageAsync("api-key", new JioCxSendRequest("message-1", "agent-1", "campaign-10", ["+918000000000"], new { content = new { plainText = "Hi" } }, sms), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.Body!);
        doc.RootElement.GetProperty("data_sms").GetProperty("sender_id").GetString().Should().Be("JIDHGD");
        doc.RootElement.GetProperty("data_sms").GetProperty("body").GetString().Should().Be("SMS Body");
    }

    [Fact]
    public async Task SendMessage_WhenJioCxTimesOut_ReturnsFailedResult()
    {
        var handler = new ThrowingHandler(new TaskCanceledException("A task was canceled."));
        var client = CreateClient(handler);

        var result = await client.SendMessageAsync("api-key", new JioCxSendRequest("message-1", "agent-1", "campaign-10", ["+918000000000"], new { content = new { plainText = "Hi" } }), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.StatusCode.Should().Be(0);
        result.ResponseJson.Should().Be("JioCX send message request timed out.");
    }

    [Fact]
    public async Task CheckCapability_SendsDocumentedHeadersAndBody()
    {
        var handler = new CaptureHandler("""{"capable":true}""");
        var client = CreateClient(handler);

        await client.CheckCapabilityAsync("api-key", "agent-1", ["+918000000000", "8369019147"], CancellationToken.None);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.PathAndQuery.Should().Be("/api/v1/checkCapability");
        handler.Request.Headers.GetValues("x-apikey").Single().Should().Be("api-key");
        handler.Request.Headers.GetValues("agentid").Single().Should().Be("agent-1");
        handler.Body.Should().NotBeNull();
        using var doc = JsonDocument.Parse(handler.Body!);
        doc.RootElement.GetProperty("PhoneNumbers").EnumerateArray().Select(value => value.GetString())
            .Should().Equal("+918000000000", "8369019147");
    }

    [Fact]
    public async Task CheckCapability_WhenJioCxTimesOut_ReturnsFailedResult()
    {
        var handler = new ThrowingHandler(new TaskCanceledException("A task was canceled."));
        var client = CreateClient(handler);

        var result = await client.CheckCapabilityAsync("api-key", "agent-1", ["+918000000000"], CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.StatusCode.Should().Be(0);
        result.ResponseJson.Should().Be("JioCX capability check request timed out.");
    }

    private static JioCxClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://rcsapi-uat.jiocx.com") };
        var options = Options.Create(new JioCxOptions());
        return new JioCxClient(httpClient, options);
    }
}

internal sealed class CaptureHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _statusCode;
    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }

    public CaptureHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _response = response;
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_response)
        };
    }
}

internal sealed class ThrowingHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHandler(Exception exception) => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(_exception);
}

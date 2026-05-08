using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using JioCxRcsWrapper.Application.Common.Options;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Security;
using Microsoft.Extensions.Options;

namespace JioCxRcsWrapper.Infrastructure.JioCx;

public sealed class JioCxClient : IJioCxClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly HttpClient _httpClient;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly JioCxOptions _options;

    public JioCxClient(HttpClient httpClient, IOptions<JioCxOptions> options, IAuditService auditService, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _auditService = auditService;
        _currentUser = currentUser;
        _options = options.Value;
        
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        _httpClient.BaseAddress ??= new Uri(baseUrl + "/");
    }

    private string BuildUrl(string path) => $"{_httpClient.BaseAddress?.ToString().TrimEnd('/')}/{path.TrimStart('/')}";

    public async Task<JioCxUploadResult> UploadFileAsync(string apiKey, string agentId, Stream file, string fileName, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            using var bufferedFile = new MemoryStream();
            await file.CopyToAsync(bufferedFile, cancellationToken);

            var path = _options.UploadFilePath;
            using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'));
            request.Headers.Add("x-apikey", apiKey);
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(bufferedFile.ToArray());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent(agentId), "agentId");
            request.Content = content;

            var curl = $"curl -X POST \"{BuildUrl(path)}\" -H \"x-apikey: {apiKey}\" -F \"file=@{fileName}\" -F \"agentId={agentId}\"";

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : 0;
            await _auditService.LogAsync(userId, "UploadMedia", "JioCX", curl, responseJson, cancellationToken);

            return new JioCxUploadResult(response.IsSuccessStatusCode, (int)response.StatusCode, responseJson, TryReadUrl(responseJson), curl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new JioCxUploadResult(false, 0, "JioCX media upload timed out.", null, null);
        }
        catch (HttpRequestException ex)
        {
            return new JioCxUploadResult(false, 0, BuildTransportFailure(ex), null, null);
        }
        catch (IOException ex)
        {
            return new JioCxUploadResult(false, 0, BuildTransportFailure(ex), null, null);
        }
    }

    public async Task<JioCxSendResult> SendMessageAsync(string apiKey, JioCxSendRequest requestModel, CancellationToken cancellationToken)
    {
        try
        {
            var path = _options.SendMessagePath;
            using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'));
            request.Headers.Add("x-apikey", apiKey);
            var payload = new
            {
                messageID = requestModel.MessageId,
                agentID = requestModel.AgentId,
                campaignID = requestModel.CampaignId,
                contacts = requestModel.Contacts,
                data = requestModel.Data,
                data_sms = requestModel.SmsFallback
            };
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            var curl = $"curl -X POST \"{BuildUrl(path)}\" -H \"x-apikey: {apiKey}\" -H \"Content-Type: application/json\" -d '{payloadJson}'";

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : 0;
            await _auditService.LogAsync(userId, "SendMessage", "JioCX", curl, responseJson, cancellationToken);

            return new JioCxSendResult(response.IsSuccessStatusCode, (int)response.StatusCode, responseJson, curl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new JioCxSendResult(false, 0, "JioCX send message request timed out.", null);
        }
        catch (HttpRequestException ex)
        {
            return new JioCxSendResult(false, 0, BuildTransportFailure(ex), null);
        }
    }

    public async Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, string phoneNumber, CancellationToken cancellationToken)
        => await CheckCapabilityAsync(apiKey, agentId, [phoneNumber], cancellationToken);

    public async Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, IReadOnlyList<string> phoneNumbers, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.CheckCapabilityPath);
            request.Headers.Add("x-apikey", apiKey);
            request.Headers.Add("agentid", agentId);
            var payload = new Dictionary<string, IReadOnlyList<string>>
            {
                ["PhoneNumbers"] = phoneNumbers
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return new JioCxCapabilityResult(response.IsSuccessStatusCode, (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new JioCxCapabilityResult(false, 0, "JioCX capability check request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return new JioCxCapabilityResult(false, 0, BuildTransportFailure(ex));
        }
    }

    private static string? TryReadUrl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryReadUrl(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildTransportFailure(Exception exception)
    {
        var innerMessage = exception.InnerException?.Message;
        return string.IsNullOrWhiteSpace(innerMessage)
            ? exception.Message
            : $"{exception.Message} Inner error: {innerMessage}";
    }

    private static string? TryReadUrl(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in new[] { "url", "fileUrl", "fileURL", "publicUrl", "publicURL" })
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var nestedUrl = TryReadUrl(property.Value);
                if (!string.IsNullOrWhiteSpace(nestedUrl))
                {
                    return nestedUrl;
                }
            }
        }

        return null;
    }
}

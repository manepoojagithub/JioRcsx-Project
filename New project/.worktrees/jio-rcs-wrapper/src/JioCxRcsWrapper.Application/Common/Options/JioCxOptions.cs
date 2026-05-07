namespace JioCxRcsWrapper.Application.Common.Options;

public sealed class JioCxOptions
{
    public string BaseUrl { get; set; } = "https://rcsapi.jiocx.com";
    public string UploadFilePath { get; set; } = "/api/v1/uploadFile";
    public string SendMessagePath { get; set; } = "/api/v1/sendMessage";
    public string CheckCapabilityPath { get; set; } = "/api/v1/checkCapability";
}

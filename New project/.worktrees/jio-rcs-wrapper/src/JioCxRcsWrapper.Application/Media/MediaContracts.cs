namespace JioCxRcsWrapper.Application.Media;

public sealed record MediaValidationResult(bool IsValid, string? Error);

public interface IMediaValidator
{
    MediaValidationResult Validate(string contentType, long sizeBytes, bool isThumbnail = false);
}

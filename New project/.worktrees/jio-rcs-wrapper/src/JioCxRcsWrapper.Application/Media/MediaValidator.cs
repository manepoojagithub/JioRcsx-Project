namespace JioCxRcsWrapper.Application.Media;

public sealed class MediaValidator : IMediaValidator
{
    private static readonly HashSet<string> ImageTypes = ["image/jpeg", "image/jpg", "image/gif", "image/png"];
    private static readonly HashSet<string> VideoTypes = ["video/mp4", "video/mpeg", "video/mpeg4", "video/webm"];
    private const long TwoMb = 2 * 1024 * 1024;
    private const long TenMb = 10 * 1024 * 1024;
    private const long FortyKb = 40 * 1024;

    public MediaValidationResult Validate(string contentType, long sizeBytes, bool isThumbnail = false)
    {
        if (isThumbnail)
        {
            return sizeBytes <= FortyKb
                ? new MediaValidationResult(true, null)
                : new MediaValidationResult(false, "Thumbnail must be less than 40 KB.");
        }

        if (ImageTypes.Contains(contentType))
        {
            return sizeBytes < TwoMb
                ? new MediaValidationResult(true, null)
                : new MediaValidationResult(false, "Image must be less than 2 MB.");
        }

        if (VideoTypes.Contains(contentType))
        {
            return sizeBytes < TenMb
                ? new MediaValidationResult(true, null)
                : new MediaValidationResult(false, "Video must be less than 10 MB.");
        }

        return new MediaValidationResult(false, "Unsupported media type.");
    }
}

namespace JioCxRcsWrapper.Application.Common.Interfaces;

public interface IApiSettingService
{
    Task<string> GetValueAsync(string key, string defaultValue = "");
    Task UpdateValueAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default);
}

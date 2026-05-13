using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Infrastructure.Security;

public sealed class ApiSettingService : IApiSettingService
{
    private readonly IUnitOfWork _unitOfWork;

    public ApiSettingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        var setting = await _unitOfWork.Repository<ApiSetting>().Query()
            .FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value ?? defaultValue;
    }

    public async Task UpdateValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await _unitOfWork.Repository<ApiSetting>().Query()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting == null)
        {
            await _unitOfWork.Repository<ApiSetting>().AddAsync(new ApiSetting
            {
                Key = key,
                Value = value,
                Description = "Dynamic setting"
            }, cancellationToken);
        }
        else
        {
            setting.Value = value;
            _unitOfWork.Repository<ApiSetting>().Update(setting);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Repository<ApiSetting>().Query()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
    }
}

using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Web.Models.ApiSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ApiSettingsController : Controller
{
    private readonly IApiSettingService _apiSettings;

    public ApiSettingsController(IApiSettingService apiSettings)
    {
        _apiSettings = apiSettings;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await _apiSettings.GetAllSettingsAsync();
        
        var model = new ApiSettingsViewModel
        {
            BaseUrl = settings.GetValueOrDefault("JioCx_BaseUrl", ""),
            UploadFilePath = settings.GetValueOrDefault("JioCx_UploadFilePath", ""),
            SendMessagePath = settings.GetValueOrDefault("JioCx_SendMessagePath", ""),
            CheckCapabilityPath = settings.GetValueOrDefault("JioCx_CheckCapabilityPath", "")
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ApiSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _apiSettings.UpdateValueAsync("JioCx_BaseUrl", model.BaseUrl, cancellationToken);
        await _apiSettings.UpdateValueAsync("JioCx_UploadFilePath", model.UploadFilePath, cancellationToken);
        await _apiSettings.UpdateValueAsync("JioCx_SendMessagePath", model.SendMessagePath, cancellationToken);
        await _apiSettings.UpdateValueAsync("JioCx_CheckCapabilityPath", model.CheckCapabilityPath, cancellationToken);

        TempData["SuccessMessage"] = "API settings updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}

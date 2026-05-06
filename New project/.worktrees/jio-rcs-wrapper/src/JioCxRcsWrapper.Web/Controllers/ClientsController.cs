using System.Security.Claims;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Pagination;
using JioCxRcsWrapper.Web.Filters;
using JioCxRcsWrapper.Web.Models.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class ClientsController : Controller
{
    private readonly IClientOnboardingService _clients;
    private readonly IWebHostEnvironment _environment;

    public ClientsController(IClientOnboardingService clients, IWebHostEnvironment environment)
    {
        _clients = clients;
        _environment = environment;
    }

    [RequirePermission("Clients", "View")]
    public async Task<IActionResult> Index(ClientFilter filter, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var clients = await _clients.ListAsync(filter, cancellationToken);
        ViewBag.TotalClients = clients.Count;
        ViewBag.ActiveAgents = clients.Count(client => !string.IsNullOrWhiteSpace(client.AgentId));
        return View(PagedResult<ClientSummary>.Create(clients, pageNumber, pageSize));
    }

    [RequirePermission("Clients", "Add")]
    public IActionResult Create()
    {
        return View(new CreateClientViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Clients", "Add")]
    public async Task<IActionResult> Create(CreateClientViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var logoPath = await SaveLogoAsync(model.LogoFile, nameof(CreateClientViewModel.LogoFile), cancellationToken) ?? model.LogoPath;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _clients.CreateAsync(new CreateClientRequest(
                model.BrandName,
                model.AgentName,
                model.AgentId,
                model.AgentKey,
                model.SiteName,
                model.ManagerName,
                model.ManagerEmail,
                model.ManagerPassword,
                logoPath,
                model.Credits,
                model.CreditCostPerMessage,
                model.LowCreditThreshold), adminUserId, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequirePermission("Clients", "Update")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var client = await _clients.GetAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        return View(new EditClientViewModel
        {
            Id = client.Id,
            BrandName = client.BrandName,
            AgentName = client.AgentName,
            AgentId = MaskSecret(client.AgentId),
            AgentKey = "********",
            SiteName = client.SiteName,
            LogoPath = client.LogoPath,
            Credits = client.Credits,
            CreditCostPerMessage = client.CreditCostPerMessage,
            LowCreditThreshold = client.LowCreditThreshold
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Clients", "Update")]
    public async Task<IActionResult> Edit(EditClientViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var logoPath = await SaveLogoAsync(model.LogoFile, nameof(EditClientViewModel.LogoFile), cancellationToken) ?? model.LogoPath;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _clients.UpdateAsync(new UpdateClientRequest(
                model.Id,
                model.BrandName,
                model.AgentName,
                model.AgentId,
                model.AgentKey,
                model.SiteName,
                logoPath,
                model.Credits,
                model.CreditCostPerMessage,
                model.LowCreditThreshold), cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Clients", "Delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _clients.DeleteAsync(id, cancellationToken);
            TempData["ClientMessage"] = "Client deleted.";
        }
        catch (Exception ex)
        {
            TempData["ClientError"] = $"Client could not be deleted. {ex.GetBaseException().Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> SaveLogoAsync(IFormFile? logoFile, string modelStateKey, CancellationToken cancellationToken)
    {
        if (logoFile is null || logoFile.Length == 0)
        {
            return null;
        }

        if (!logoFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(modelStateKey, "Logo must be an image.");
            return null;
        }

        var extension = Path.GetExtension(logoFile.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "logos");
        Directory.CreateDirectory(uploadRoot);
        var absolutePath = Path.Combine(uploadRoot, fileName);
        await using var stream = System.IO.File.Create(absolutePath);
        await logoFile.CopyToAsync(stream, cancellationToken);
        return $"/uploads/logos/{fileName}";
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "********";
        }

        return value.Length <= 4 ? "********" : $"********{value[^4..]}";
    }
}

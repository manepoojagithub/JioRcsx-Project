using System.Text;
using JioCxRcsWrapper.Application.Campaigns;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Pagination;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Application.Templates;
using JioCxRcsWrapper.Web.Filters;
using JioCxRcsWrapper.Web.Models.Campaigns;
using Microsoft.AspNetCore.Authorization;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class CampaignsController : Controller
{
    private readonly ICampaignService _campaigns;
    private readonly ICurrentUser _currentUser;
    private readonly IMessageTemplateService _templates;
    private readonly IClientOnboardingService _clients;
    private readonly IUnitOfWork _unitOfWork;

    public CampaignsController(ICampaignService campaigns, ICurrentUser currentUser, IMessageTemplateService templates, IClientOnboardingService clients, IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _currentUser = currentUser;
        _templates = templates;
        _clients = clients;
        _unitOfWork = unitOfWork;
    }

    [RequirePermission("Campaigns", "View")]
    public async Task<IActionResult> Index(CampaignFilter filter, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var campaigns = await _campaigns.ListAsync(filter, cancellationToken);
        ViewBag.TotalCampaigns = campaigns.Count;
        ViewBag.TotalContacts = campaigns.Sum(campaign => campaign.ContactCount);
        ViewBag.QueuedCampaigns = campaigns.Count(campaign => campaign.Status.ToString().Contains("Queued"));
        ViewBag.ScheduledCampaigns = campaigns.Count(campaign => campaign.ScheduledAt is not null);
        return View(PagedResult<CampaignSummary>.Create(campaigns, pageNumber, pageSize));
    }

    [RequirePermission("Campaigns", "View")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var campaigns = await _campaigns.ListAsync(null, cancellationToken);
        var campaign = campaigns.FirstOrDefault(c => c.Id == id);
        if (campaign == null) return NotFound();

        var contacts = await _campaigns.GetContactsAsync(id, cancellationToken);
        
        MessageTemplateEditor? template = null;
        var campaignMessage = _unitOfWork.Repository<CampaignMessage>().Query().FirstOrDefault(m => m.CampaignId == id);
        if (campaignMessage is not null && campaignMessage.TemplateId.HasValue)
        {
            template = await _templates.GetForEditAsync(campaignMessage.TemplateId.Value, cancellationToken);
        }

        var viewModel = new DetailsViewModel
        {
            Campaign = campaign,
            Contacts = contacts,
            Template = template
        };

        return View(viewModel);
    }

    [RequirePermission("Campaigns", "Add")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateLookupsAsync(cancellationToken);
        return View(new CreateCampaignViewModel { ClientId = _currentUser.ClientId ?? 0, IsRCSEnabled = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Add")]
    public async Task<IActionResult> Create(CreateCampaignViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(cancellationToken);
            return View(model);
        }

        var result = await _campaigns.CreateDraftAsync(
            new CreateCampaignRequest(model.Name, model.ClientId, model.Type, model.ScheduledAt, model.TemplateId, [model.PhoneNumbers ?? string.Empty], model.IsRCSEnabled),
            cancellationToken);

        if (!result.IsSuccess)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await PopulateLookupsAsync(cancellationToken);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [RequirePermission("Campaigns", "Download")]
    public IActionResult DownloadCsvTemplate()
    {
        return File(Encoding.UTF8.GetBytes("MobileNumber\r\n+918000000000\r\n"), "text/csv", "contacts-template.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Add")]
    public async Task<IActionResult> UploadContacts(UploadContactsViewModel model, CancellationToken cancellationToken)
    {
        if (model.CsvFile is null)
        {
            return BadRequest(new { errors = new[] { "Contact required" } });
        }

        await using var stream = model.CsvFile.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csv = await reader.ReadToEndAsync(cancellationToken);
        var result = await _campaigns.UploadContactsAsync(model.CampaignId, csv, cancellationToken);
        return ToJsonResult(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Update")]
    public async Task<IActionResult> Queue(int campaignId, CancellationToken cancellationToken)
    {
        var result = await _campaigns.QueueCampaignAsync(campaignId, cancellationToken);
        return ToJsonResult(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Update")]
    public async Task<IActionResult> Disable(int campaignId, CancellationToken cancellationToken)
    {
        var result = await _campaigns.DisableCampaignAsync(campaignId, cancellationToken);
        return ToJsonResult(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Retry")]
    public async Task<IActionResult> RetryFailed(int campaignId, CancellationToken cancellationToken)
    {
        var result = await _campaigns.RetryFailedAsync(campaignId, cancellationToken);
        return ToJsonResult(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Update")]
    public async Task<IActionResult> DeleteContacts(int campaignId, int[] contactIds, CancellationToken cancellationToken)
    {
        var result = await _campaigns.DeleteContactsAsync(campaignId, contactIds, cancellationToken);
        return ToJsonResult(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Retry")]
    public async Task<IActionResult> RetrySelected(int campaignId, int[] contactIds, CancellationToken cancellationToken)
    {
        var result = await _campaigns.RetryContactsAsync(campaignId, contactIds, cancellationToken);
        return ToJsonResult(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Campaigns", "Delete")]
    public async Task<IActionResult> Delete(int campaignId, CancellationToken cancellationToken)
    {
        var result = await _campaigns.DeleteCampaignAsync(campaignId, cancellationToken);
        return ToJsonResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts(int campaignId, CancellationToken cancellationToken)
    {
        var contacts = await _campaigns.GetContactsAsync(campaignId, cancellationToken);
        return Json(contacts);
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplate(int id, CancellationToken cancellationToken)
    {
        var template = await _templates.GetForEditAsync(id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return Json(template);
    }

    private IActionResult ToJsonResult(CampaignOperationResult result)
    {
        if (result.IsSuccess)
        {
            return Json(new { id = result.Id });
        }

        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Json(new { errors = result.Errors });
    }

    private async Task PopulateLookupsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Templates = await _templates.ListAsync(null, cancellationToken);

        var clients = await _clients.ListAsync(null, cancellationToken);
        if (!string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) && _currentUser.ClientId is not null)
        {
            clients = clients.Where(client => client.Id == _currentUser.ClientId.Value).ToArray();
        }

        ViewBag.Clients = clients
            .Select(client => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(client.BrandName, client.Id.ToString()))
            .Prepend(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem("Select client", string.Empty))
            .ToArray();
    }
}

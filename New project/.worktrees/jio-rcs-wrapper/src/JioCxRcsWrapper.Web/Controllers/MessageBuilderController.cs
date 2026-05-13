using JioCxRcsWrapper.Application.Common.Pagination;
using JioCxRcsWrapper.Application.Messages;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;
using JioCxRcsWrapper.Web.Models.MessageBuilder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Clients;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class MessageBuilderController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessagePayloadService _payloadService;
    private readonly IWebHostEnvironment _environment;
    private readonly IJioCxClient _jioCx;
    private readonly ISecretProtector _protector;

    public MessageBuilderController(
        IUnitOfWork unitOfWork, 
        IMessagePayloadService payloadService, 
        IWebHostEnvironment environment,
        IJioCxClient jioCx,
        ISecretProtector protector)
    {
        _unitOfWork = unitOfWork;
        _payloadService = payloadService;
        _environment = environment;
        _jioCx = jioCx;
        _protector = protector;
    }

    public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 12, CancellationToken cancellationToken = default)
    {
        var templates = await _unitOfWork.Repository<MessageTemplate>().Query()
            .OrderByDescending(t => t.Id)
            .ToListAsync(cancellationToken);

        var clients = await _unitOfWork.Repository<Client>().Query().ToDictionaryAsync(c => c.Id, c => c.AgentName, cancellationToken);
        ViewBag.Clients = clients;

        ViewBag.PlainText = templates.Count(t => t.MessageType == MessageType.PlainText);
        ViewBag.StandaloneCards = templates.Count(t => t.MessageType == MessageType.StandaloneCard);
        ViewBag.Carousels = templates.Count(t => t.MessageType == MessageType.Carousel);

        return View(PagedResult<MessageTemplate>.Create(templates, pageNumber, pageSize));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.ClientList = await GetClientListAsync();
        return View(new MessageBuilderViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        if (model.MessageType == "PlainText")
        {
            if (string.IsNullOrWhiteSpace(model.Text)) ModelState.AddModelError(nameof(model.Text), "Text is required.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ClientList = await GetClientListAsync();
            return View(model);
        }

        // Process media uploads to JioCX first
        await ProcessMediaUploadsAsync(model, cancellationToken);

        var payloadResult = await BuildPayloadAsync(model, cancellationToken);
        if (!payloadResult.IsValid)
        {
            foreach (var err in payloadResult.Errors) ModelState.AddModelError(string.Empty, err);
            ViewBag.ClientList = await GetClientListAsync();
            return View(model);
        }

        var template = new MessageTemplate
        {
            Name = model.TemplateName!,
            MessageType = Enum.Parse<MessageType>(model.MessageType),
            ClientId = model.ClientId,
            PayloadJson = payloadResult.PayloadJson,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
        };

        // Capture local media info from the first card if available for internal tracking
        if (model.Cards.Count > 0)
        {
            template.LocalMediaPath = model.Cards[0].LocalMediaPath;
            template.MediaContentType = model.Cards[0].MediaContentType;
            template.RcsMediaUrl = model.Cards[0].MediaUrl; // The JioCX URL
        }

        await _unitOfWork.Repository<MessageTemplate>().AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.Repository<MessageTemplate>().GetByIdAsync(id, cancellationToken);
        if (template is null) return NotFound();

        var model = new MessageBuilderViewModel
        {
            Id = template.Id,
            TemplateName = template.Name,
            MessageType = template.MessageType.ToString(),
            ClientId = template.ClientId
        };

        try
        {
            ParsePayloadIntoModel(template.PayloadJson, model);
        }
        catch (Exception)
        {
            model.Text = "Error parsing template details. The JSON structure might be incompatible.";
        }

        ViewBag.ClientList = await GetClientListAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ClientList = await GetClientListAsync();
            return View(model);
        }

        var template = await _unitOfWork.Repository<MessageTemplate>().GetByIdAsync(model.Id!.Value, cancellationToken);
        if (template is null) return NotFound();

        await ProcessMediaUploadsAsync(model, cancellationToken);

        var payloadResult = await BuildPayloadAsync(model, cancellationToken);
        if (!payloadResult.IsValid)
        {
            foreach (var err in payloadResult.Errors) ModelState.AddModelError(string.Empty, err);
            ViewBag.ClientList = await GetClientListAsync();
            return View(model);
        }

        template.Name = model.TemplateName!;
        template.PayloadJson = payloadResult.PayloadJson;
        template.ClientId = model.ClientId;

        if (model.Cards.Count > 0)
        {
            template.LocalMediaPath = model.Cards[0].LocalMediaPath;
            template.MediaContentType = model.Cards[0].MediaContentType;
            template.RcsMediaUrl = model.Cards[0].MediaUrl;
        }

        _unitOfWork.Repository<MessageTemplate>().Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.Repository<MessageTemplate>().GetByIdAsync(id, cancellationToken);
        if (template != null)
        {
            _unitOfWork.Repository<MessageTemplate>().Remove(template);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task ProcessMediaUploadsAsync(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        if (model.MessageType == "PlainText") return;

        // We need an agent to upload media to JioCX.
        // If a client is assigned to the template, use their credentials.
        // If not, we might need a default admin agent or block upload.
        Client? client = null;
        if (model.ClientId.HasValue)
        {
            client = await _unitOfWork.Repository<Client>().GetByIdAsync(model.ClientId.Value, cancellationToken);
        }

        foreach (var card in model.Cards)
        {
            if (card.MediaFile != null)
            {
                // 1. Save locally
                var localPath = await SaveMediaToDiskAsync(card.MediaFile, cancellationToken);
                card.LocalMediaPath = localPath;
                card.MediaContentType = card.MediaFile.ContentType;

                // 2. Upload to JioCX if we have a client
                if (client != null)
                {
                    var absoluteLocalPath = Path.Combine(_environment.WebRootPath, localPath.TrimStart('/'));
                    using var stream = System.IO.File.OpenRead(absoluteLocalPath);
                    var uploadResult = await _jioCx.UploadFileAsync(
                        _protector.Unprotect(client.ApiKey),
                        client.AgentId,
                        stream,
                        card.MediaFile.FileName,
                        card.MediaFile.ContentType,
                        cancellationToken);

                    if (uploadResult.Succeeded && !string.IsNullOrWhiteSpace(uploadResult.PublicUrl))
                    {
                        card.MediaUrl = uploadResult.PublicUrl;
                    }
                }
                else
                {
                    // If no client, we can't get a JioCX URL. Fallback to local (which won't work in real RCS send).
                    card.MediaUrl = card.LocalMediaPath;
                }
            }
        }
    }

    private async Task<MessagePayloadResult> BuildPayloadAsync(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        if (model.MessageType == "PlainText")
        {
            return _payloadService.BuildPlainText(model.Text!);
        }

        var drafts = model.Cards.Select(card => new StandaloneCardDraft(
            card.Title,
            card.Description,
            card.MediaUrl,
            card.ThumbnailUrl,
            card.Ctas.Select(c => new CtaDraft(c.Text, c.ActionType, c.Value, c.PostBackData)).ToList()
        )).ToList();

        return model.MessageType == "StandaloneCard"
            ? _payloadService.BuildStandaloneCard(drafts[0])
            : _payloadService.BuildCarousel(new CarouselDraft(drafts));
    }

    private void ParsePayloadIntoModel(string json, MessageBuilderViewModel model)
    {
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content");

        if (content.TryGetProperty("text", out var textProp))
        {
            model.Text = textProp.GetString();
            return;
        }

        if (content.TryGetProperty("richCardDetails", out var richCard))
        {
            if (richCard.TryGetProperty("standalone", out var standalone))
            {
                model.Cards = [MapJsonToCard(standalone.GetProperty("cardContent"))];
            }
            else if (richCard.TryGetProperty("carousel", out var carousel))
            {
                model.Cards = carousel.GetProperty("cardContents").EnumerateArray()
                    .Select(MapJsonToCard).ToList();
            }
        }
    }

    private MessageCardViewModel MapJsonToCard(JsonElement json)
    {
        var card = new MessageCardViewModel
        {
            Title = json.TryGetProperty("title", out var t) ? t.GetString() : null,
            Description = json.TryGetProperty("description", out var d) ? d.GetString() : null,
        };

        if (json.TryGetProperty("media", out var media))
        {
            card.MediaUrl = media.TryGetProperty("contentUrl", out var cu) ? cu.GetString() : null;
            card.ThumbnailUrl = media.TryGetProperty("thumbnailUrl", out var tu) ? tu.GetString() : null;
        }

        if (json.TryGetProperty("suggestions", out var suggestions))
        {
            foreach (var s in suggestions.EnumerateArray())
            {
                var action = s.GetProperty("action");
                var cta = new CtaViewModel { Text = action.GetProperty("displayText").GetString()! };
                
                if (action.TryGetProperty("openUrlAction", out var openUrl))
                {
                    cta.ActionType = CtaActionType.OpenUrl;
                    cta.Value = openUrl.GetProperty("url").GetString()!;
                }
                else if (action.TryGetProperty("dialerAction", out var dialer))
                {
                    cta.ActionType = CtaActionType.Dialer;
                    cta.Value = dialer.GetProperty("phoneNumber").GetString()!;
                }
                card.Ctas.Add(cta);
            }
        }

        return card;
    }

    private async Task<string> SaveMediaToDiskAsync(IFormFile mediaFile, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(mediaFile.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "media");
        Directory.CreateDirectory(uploadRoot);
        var absolutePath = Path.Combine(uploadRoot, fileName);
        await using var stream = System.IO.File.Create(absolutePath);
        await mediaFile.CopyToAsync(stream, cancellationToken);
        return $"/uploads/media/{fileName}";
    }

    private async Task<IEnumerable<SelectListItem>> GetClientListAsync()
    {
        var query = _unitOfWork.Repository<Client>().Query();
        
        if (!string.Equals(User.FindFirstValue(ClaimTypes.Role), "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var userClientId = int.TryParse(User.FindFirstValue("client_id"), out var cid) ? cid : (int?)null;
            query = query.Where(c => c.Id == userClientId);
        }

        var clients = await query.OrderBy(c => c.BrandName).ToListAsync();
        return clients.Select(c => new SelectListItem($"{c.BrandName} ({c.AgentUseCase})", c.Id.ToString()));
    }
}

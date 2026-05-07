using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Common.Pagination;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Media;
using JioCxRcsWrapper.Application.Messages;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Application.Templates;
using JioCxRcsWrapper.Domain.Enums;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Web.Filters;
using JioCxRcsWrapper.Web.Models.MessageBuilder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class MessageBuilderController : Controller
{
    private readonly IMessagePayloadService _payloadService;
    private readonly IMessageTemplateService _templates;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly IJioCxClient _jioCxClient;
    private readonly IMediaValidator _mediaValidator;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MessageBuilderController> _logger;

    public MessageBuilderController(
        IMessagePayloadService payloadService,
        IMessageTemplateService templates,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        IJioCxClient jioCxClient,
        IMediaValidator mediaValidator,
        IWebHostEnvironment environment,
        ILogger<MessageBuilderController> logger)
    {
        _payloadService = payloadService;
        _templates = templates;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _jioCxClient = jioCxClient;
        _mediaValidator = mediaValidator;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission("MessageBuilder", "View")]
    public async Task<IActionResult> Index(MessageTemplateFilter filter, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var templates = await _templates.ListAsync(filter, cancellationToken);
        ViewBag.TemplateCount = templates.Count;
        ViewBag.RichCards = templates.Count(template => template.MessageType.ToString() == "RichCard");
        ViewBag.MediaReady = templates.Count(template => !string.IsNullOrWhiteSpace(template.RcsMediaUrl));
        return View(PagedResult<MessageTemplateSummary>.Create(templates, pageNumber, pageSize));
    }

    [HttpGet]
    [RequirePermission("MessageBuilder", "Add")]
    public IActionResult Create()
    {
        PopulateClients();
        return View(new MessageBuilderViewModel());
    }

    [HttpGet]
    [RequirePermission("MessageBuilder", "Update")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var template = await _templates.GetForEditAsync(id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        PopulateClients();
        return View(ToBuilderModel(template));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MessageBuilder", "Add")]
    public IActionResult Preview(MessageBuilderViewModel model)
    {
        ApplyPreviewMediaPlaceholder(model);
        var result = BuildPayload(model);

        if (!result.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = result.Errors });
        }

        return Json(new { payloadJson = result.PayloadJson });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MessageBuilder", "Add")]
    public async Task<IActionResult> SaveTemplate(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            return await SaveTemplateCoreAsync(model, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            return Json(new { errors = new[] { "Template save timed out while waiting for an external service. Please try again." } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to save message template {TemplateName}", model.TemplateName);
            Response.StatusCode = StatusCodes.Status500InternalServerError;

            var message = "Unable to save template. Please check the server log for details.";
            if (_environment.IsDevelopment())
            {
                message = $"Unable to save template. {ex.GetBaseException().Message}";
            }

            return Json(new { errors = new[] { message } });
        }
    }

    private async Task<IActionResult> SaveTemplateCoreAsync(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.TemplateName))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = new[] { "Template name is required." } });
        }

        var mediaResult = await SaveAndUploadMediaForTemplateAsync(model, cancellationToken);
        if (!mediaResult.IsSuccess)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = new[] { mediaResult.Error } });
        }

        if (!string.IsNullOrWhiteSpace(mediaResult.RcsUrl))
        {
            model.MediaUrl = mediaResult.RcsUrl;
        }

        var result = BuildPayload(model);
        if (!result.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = result.Errors });
        }

        var messageType = model.MessageType == "RichCard" ? MessageType.RichCard : MessageType.PlainText;
        var id = await _templates.CreateAsync(new CreateMessageTemplateRequest(
            model.TemplateName,
            messageType,
            result.PayloadJson!,
            ResolveClientId(model),
            mediaResult.LocalPath,
            mediaResult.RcsUrl,
            mediaResult.ContentType), cancellationToken);
        return Json(new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MessageBuilder", "Update")]
    public async Task<IActionResult> UpdateTemplate(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = new[] { "Template id is required." } });
        }

        if (string.IsNullOrWhiteSpace(model.TemplateName))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = new[] { "Template name is required." } });
        }

        var mediaResult = await SaveAndUploadMediaForTemplateAsync(model, cancellationToken);
        if (!mediaResult.IsSuccess)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = new[] { mediaResult.Error } });
        }

        if (!string.IsNullOrWhiteSpace(mediaResult.RcsUrl))
        {
            model.MediaUrl = mediaResult.RcsUrl;
        }

        var result = BuildPayload(model);
        if (!result.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { errors = result.Errors });
        }

        var messageType = model.MessageType == "RichCard" ? MessageType.RichCard : MessageType.PlainText;
        await _templates.UpdateAsync(new UpdateMessageTemplateRequest(
            model.Id.Value,
            model.TemplateName,
            messageType,
            result.PayloadJson!,
            ResolveClientId(model),
            mediaResult.LocalPath,
            mediaResult.RcsUrl,
            mediaResult.ContentType), cancellationToken);
        return Json(new { id = model.Id.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MessageBuilder", "Delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _templates.DeleteAsync(id, cancellationToken);
            TempData["SuccessMessage"] = "Template deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequirePermission("MessageBuilder", "Add")]
    public IActionResult CtaRow(int index)
    {
        if (index < 0 || index > 3)
        {
            return BadRequest();
        }

        ViewData["Index"] = index;
        return PartialView("_CtaRow", new CtaViewModel { ActionType = CtaActionType.OpenUrl });
    }

    private MessagePayloadResult BuildPayload(MessageBuilderViewModel model)
    {
        return model.MessageType == "RichCard"
            ? _payloadService.BuildRichCard(new RichCardDraft(
                model.Title,
                model.Description,
                model.Footer,
                model.MediaUrl,
                model.ThumbnailUrl,
                model.Ctas.Select(cta => new CtaDraft(cta.Text, cta.ActionType, cta.Value, cta.PostBackData)).ToArray()))
            : _payloadService.BuildPlainText(new PlainTextMessageDraft(model.Text ?? string.Empty));
    }

    private int? ResolveClientId(MessageBuilderViewModel model) => _currentUser.ClientId ?? model.ClientId;

    private static void ApplyPreviewMediaPlaceholder(MessageBuilderViewModel model)
    {
        if (model.MessageType != "RichCard" ||
            model.MediaFile is null ||
            model.MediaFile.Length == 0 ||
            !string.IsNullOrWhiteSpace(model.MediaUrl))
        {
            return;
        }

        var fileName = Uri.EscapeDataString(Path.GetFileName(model.MediaFile.FileName));
        model.MediaUrl = $"https://preview.advaitservices.local/rcs-media/{fileName}";
    }

    private async Task<TemplateMediaResult> SaveAndUploadMediaForTemplateAsync(MessageBuilderViewModel model, CancellationToken cancellationToken)
    {
        if (model.MessageType != "RichCard" || model.MediaFile is null || model.MediaFile.Length == 0)
        {
            return TemplateMediaResult.Success(model.LocalMediaPath, model.MediaUrl, model.MediaContentType);
        }

        var validation = _mediaValidator.Validate(model.MediaFile.ContentType, model.MediaFile.Length);
        if (!validation.IsValid)
        {
            return TemplateMediaResult.Failed(validation.Error ?? "Invalid media file.");
        }

        var clientId = ResolveClientId(model);
        if (clientId is null)
        {
            return TemplateMediaResult.Failed("Client is required to upload template media to JioCX.");
        }

        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(clientId.Value, cancellationToken);
        if (client is null)
        {
            return TemplateMediaResult.Failed("Client not found.");
        }

        var extension = Path.GetExtension(model.MediaFile.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "rcs-media");
        Directory.CreateDirectory(uploadRoot);
        var absolutePath = Path.Combine(uploadRoot, fileName);
        await using (var localStream = System.IO.File.Create(absolutePath))
        {
            await model.MediaFile.CopyToAsync(localStream, cancellationToken);
        }

        await using var uploadStream = System.IO.File.OpenRead(absolutePath);
        var uploadResult = await _jioCxClient.UploadFileAsync(
            _secretProtector.Unprotect(client.ApiKey),
            client.AgentId,
            uploadStream,
            model.MediaFile.FileName,
            model.MediaFile.ContentType,
            cancellationToken);

        if (!uploadResult.Succeeded || string.IsNullOrWhiteSpace(uploadResult.PublicUrl))
        {
            return TemplateMediaResult.Failed(BuildUploadError(uploadResult));
        }

        var localPath = $"/uploads/rcs-media/{fileName}";
        await _unitOfWork.Repository<UploadedMedia>().AddAsync(new UploadedMedia
        {
            ClientId = client.Id,
            FileName = model.MediaFile.FileName,
            ContentType = model.MediaFile.ContentType,
            SizeBytes = model.MediaFile.Length,
            LocalPath = localPath,
            PublicUrl = uploadResult.PublicUrl,
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return TemplateMediaResult.Success(localPath, uploadResult.PublicUrl, model.MediaFile.ContentType);
    }

    private static string BuildUploadError(JioCxUploadResult uploadResult)
    {
        var response = string.IsNullOrWhiteSpace(uploadResult.ResponseJson)
            ? "No response body returned."
            : uploadResult.ResponseJson.Trim();

        if (response.Length > 500)
        {
            response = $"{response[..500]}...";
        }

        if (uploadResult.Succeeded)
        {
            return $"JioCX media upload succeeded but no public media URL was returned. Response: {response}";
        }

        return $"JioCX media upload failed (HTTP {uploadResult.StatusCode}). Response: {response}";
    }

    private void PopulateClients()
    {
        ViewBag.Clients = _unitOfWork.Repository<Client>().Query()
            .OrderBy(client => client.BrandName)
            .Select(client => new SelectListItem(client.BrandName, client.Id.ToString()))
            .ToArray();
    }

    private static MessageBuilderViewModel ToBuilderModel(MessageTemplateEditor template)
    {
        var model = new MessageBuilderViewModel
        {
            Id = template.Id,
            TemplateName = template.Name,
            ClientId = template.ClientId,
            MessageType = template.MessageType.ToString(),
            LocalMediaPath = template.LocalMediaPath,
            MediaUrl = template.RcsMediaUrl,
            MediaContentType = template.MediaContentType
        };

        try
        {
            using var document = JsonDocument.Parse(template.PayloadJson);
            var content = document.RootElement.GetProperty("content");
            if (template.MessageType == MessageType.PlainText)
            {
                model.Text = content.GetProperty("plainText").GetString();
                return model;
            }

            var cardContent = content
                .GetProperty("richCardDetails")
                .GetProperty("standalone")
                .GetProperty("content");

            model.Title = cardContent.GetProperty("cardTitle").GetString();
            model.Description = cardContent.GetProperty("cardDescription").GetString();
            if (cardContent.TryGetProperty("cardFooter", out var footer))
            {
                model.Footer = footer.GetString();
            }
            if (cardContent.TryGetProperty("cardMedia", out var media) &&
                media.TryGetProperty("contentInfo", out var contentInfo) &&
                contentInfo.TryGetProperty("fileUrl", out var fileUrl))
            {
                model.MediaUrl = fileUrl.GetString();
            }

            if (cardContent.TryGetProperty("suggestions", out var suggestions) && suggestions.ValueKind == JsonValueKind.Array)
            {
                model.Ctas = suggestions.EnumerateArray().Select(TryReadCta).Where(cta => cta is not null).Cast<CtaViewModel>().ToList();
            }
        }
        catch (JsonException)
        {
        }
        catch (KeyNotFoundException)
        {
        }

        return model;
    }

    private static CtaViewModel? TryReadCta(JsonElement suggestion)
    {
        if (suggestion.TryGetProperty("reply", out var reply))
        {
            return new CtaViewModel
            {
                ActionType = CtaActionType.SuggestedReply,
                Text = ReadString(reply, "plainText"),
                PostBackData = ReadPostback(reply)
            };
        }

        if (!suggestion.TryGetProperty("action", out var action))
        {
            return null;
        }

        var cta = new CtaViewModel
        {
            Text = ReadString(action, "plainText"),
            PostBackData = ReadPostback(action),
            ActionType = CtaActionType.OpenUrl
        };

        if (action.TryGetProperty("openUrl", out var openUrl))
        {
            cta.ActionType = CtaActionType.OpenUrl;
            cta.Value = ReadString(openUrl, "url");
        }
        else if (action.TryGetProperty("dialerAction", out var dialer))
        {
            cta.ActionType = CtaActionType.Dialer;
            cta.Value = ReadString(dialer, "phoneNumber");
        }
        else if (action.TryGetProperty("createCalendarEvent", out var calendar))
        {
            cta.ActionType = CtaActionType.Calendar;
            cta.Value = calendar.GetRawText();
        }
        else if (action.TryGetProperty("showLocation", out var location))
        {
            cta.ActionType = CtaActionType.Location;
            cta.Value = location.GetRawText();
        }

        return cta;
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string ReadPostback(JsonElement element)
    {
        return element.TryGetProperty("postBack", out var postBack) &&
            postBack.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.String
            ? data.GetString() ?? string.Empty
            : string.Empty;
    }

    private sealed record TemplateMediaResult(bool IsSuccess, string? LocalPath, string? RcsUrl, string? ContentType, string? Error)
    {
        public static TemplateMediaResult Success(string? localPath, string? rcsUrl, string? contentType) => new(true, localPath, rcsUrl, contentType, null);

        public static TemplateMediaResult Failed(string error) => new(false, null, null, null, error);
    }
}

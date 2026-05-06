using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Media;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class MediaController : Controller
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly IJioCxClient _jioCxClient;
    private readonly IMediaValidator _mediaValidator;

    public MediaController(ICurrentUser currentUser, IUnitOfWork unitOfWork, ISecretProtector secretProtector, IJioCxClient jioCxClient, IMediaValidator mediaValidator)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _jioCxClient = jioCxClient;
        _mediaValidator = mediaValidator;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Media", "Add")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (_currentUser.ClientId is null)
        {
            return BadRequest(new { error = "Client context is required for media upload." });
        }

        var validation = _mediaValidator.Validate(file.ContentType, file.Length);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = validation.Error });
        }

        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(_currentUser.ClientId.Value, cancellationToken);
        if (client is null)
        {
            return BadRequest(new { error = "Client not found." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _jioCxClient.UploadFileAsync(_secretProtector.Unprotect(client.ApiKey), client.AgentId, stream, file.FileName, file.ContentType, cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.PublicUrl))
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "JioCX media upload failed.", response = result.ResponseJson });
        }

        var media = new UploadedMedia
        {
            ClientId = client.Id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            PublicUrl = result.PublicUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _unitOfWork.Repository<UploadedMedia>().AddAsync(media, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Json(new { url = media.PublicUrl });
    }
}

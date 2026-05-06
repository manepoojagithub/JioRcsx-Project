using System.Text;
using JioCxRcsWrapper.Application.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Controllers;

[AllowAnonymous]
[Route("webhooks")]
public sealed class WebhooksController : Controller
{
    private readonly IWebhookService _webhooks;

    public WebhooksController(IWebhookService webhooks)
    {
        _webhooks = webhooks;
    }

    [HttpPost("jiocx")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> JioCx(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rawJson = await reader.ReadToEndAsync(cancellationToken);
        await _webhooks.ProcessAsync(rawJson, cancellationToken);
        return Ok();
    }
}

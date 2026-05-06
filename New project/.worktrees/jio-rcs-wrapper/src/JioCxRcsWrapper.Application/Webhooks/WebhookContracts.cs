namespace JioCxRcsWrapper.Application.Webhooks;

public interface IWebhookService
{
    Task ProcessAsync(string rawJson, CancellationToken cancellationToken);
}

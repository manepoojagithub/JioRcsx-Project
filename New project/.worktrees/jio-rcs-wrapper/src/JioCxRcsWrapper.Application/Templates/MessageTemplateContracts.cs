using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Templates;

public sealed record MessageTemplateSummary(int Id, string Name, int? ClientId, string ClientName, MessageType MessageType, string? LocalMediaPath, string? RcsMediaUrl);

public sealed record MessageTemplateEditor(int Id, string Name, int? ClientId, MessageType MessageType, string PayloadJson, string? LocalMediaPath, string? RcsMediaUrl, string? MediaContentType);

public sealed record CreateMessageTemplateRequest(string Name, MessageType MessageType, string PayloadJson, int? ClientId, string? LocalMediaPath, string? RcsMediaUrl, string? MediaContentType);

public sealed record UpdateMessageTemplateRequest(int Id, string Name, MessageType MessageType, string PayloadJson, int? ClientId, string? LocalMediaPath, string? RcsMediaUrl, string? MediaContentType);

public sealed record MessageTemplateFilter(string? Name = null, MessageType? MessageType = null, string? ClientName = null);

public interface IMessageTemplateService
{
    Task<IReadOnlyList<MessageTemplateSummary>> ListAsync(MessageTemplateFilter? filter = null, CancellationToken cancellationToken = default);
...
    Task<int> CreateAsync(CreateMessageTemplateRequest request, CancellationToken cancellationToken = default);

    Task<MessageTemplateEditor?> GetForEditAsync(int id, CancellationToken cancellationToken = default);

    Task UpdateAsync(UpdateMessageTemplateRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

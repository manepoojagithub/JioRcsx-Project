using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Templates;

public sealed class MessageTemplateService : IMessageTemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public MessageTemplateService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<MessageTemplateSummary>> ListAsync(MessageTemplateFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var templates = _unitOfWork.Repository<MessageTemplate>().Query();
        if (!string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            templates = templates.Where(template => template.ClientId == null || template.ClientId == _currentUser.ClientId);
        }

        var clientNames = _unitOfWork.Repository<Client>().Query()
            .ToDictionary(client => client.Id, client => client.BrandName);

        var result = templates.OrderBy(template => template.Name)
            .ToArray()
            .Select(template => new MessageTemplateSummary(
                template.Id,
                template.Name,
                template.ClientId,
                template.ClientId is null ? "Global" : clientNames.GetValueOrDefault(template.ClientId.Value, "-"),
                template.MessageType,
                template.LocalMediaPath,
                template.RcsMediaUrl))
            .AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Name))
                result = result.Where(x => x.Name.Contains(filter.Name, StringComparison.OrdinalIgnoreCase));

            if (filter.MessageType.HasValue)
                result = result.Where(x => x.MessageType == filter.MessageType.Value);

            if (!string.IsNullOrWhiteSpace(filter.ClientName))
                result = result.Where(x => x.ClientName.Contains(filter.ClientName, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<MessageTemplateSummary>>(result.ToArray());
    }

    public async Task<int> CreateAsync(CreateMessageTemplateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            throw new ArgumentException("Payload is required.");
        }

        if (!Enum.IsDefined(typeof(MessageType), request.MessageType))
        {
            throw new ArgumentException("Invalid message type.");
        }

        var clientId = request.ClientId ?? (string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ? null : _currentUser.ClientId);
        var existing = _unitOfWork.Repository<MessageTemplate>().Query()
            .Any(t => t.ClientId == clientId && t.Name == request.Name.Trim() && t.MessageType == request.MessageType);
        
        if (existing)
        {
            throw new ArgumentException($"A template with the name '{request.Name.Trim()}' and type '{request.MessageType}' already exists for this client.");
        }

        var template = new MessageTemplate
        {
            Name = request.Name.Trim(),
            ClientId = clientId,
            MessageType = request.MessageType,
            PayloadJson = request.PayloadJson,
            LocalMediaPath = request.LocalMediaPath,
            RcsMediaUrl = request.RcsMediaUrl,
            MediaContentType = request.MediaContentType,
            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<MessageTemplate>().AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return template.Id;
    }

    public async Task<MessageTemplateEditor?> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.Repository<MessageTemplate>().GetByIdAsync(id, cancellationToken);
        if (template is null || !CanAccess(template))
        {
            return null;
        }

        return new MessageTemplateEditor(
            template.Id,
            template.Name,
            template.ClientId,
            template.MessageType,
            template.PayloadJson,
            template.LocalMediaPath,
            template.RcsMediaUrl,
            template.MediaContentType);
    }

    public async Task UpdateAsync(UpdateMessageTemplateRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Name, request.MessageType, request.PayloadJson);
        var template = await _unitOfWork.Repository<MessageTemplate>().GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Template not found.");

        if (!CanAccess(template))
        {
            throw new InvalidOperationException("Template is outside the current user's scope.");
        }

        var clientId = request.ClientId ?? (string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ? null : _currentUser.ClientId);
        var existing = _unitOfWork.Repository<MessageTemplate>().Query()
            .Any(t => t.Id != request.Id && t.ClientId == clientId && t.Name == request.Name.Trim() && t.MessageType == request.MessageType);

        if (existing)
        {
            throw new ArgumentException($"Another template with the name '{request.Name.Trim()}' and type '{request.MessageType}' already exists for this client.");
        }

        template.Name = request.Name.Trim();
        template.ClientId = clientId;
        template.MessageType = request.MessageType;
        template.PayloadJson = request.PayloadJson;
        template.LocalMediaPath = request.LocalMediaPath;
        template.RcsMediaUrl = request.RcsMediaUrl;
        template.MediaContentType = request.MediaContentType;

        _unitOfWork.Repository<MessageTemplate>().Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.Repository<MessageTemplate>().GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Template not found.");

        if (!CanAccess(template))
        {
            throw new InvalidOperationException("Template is outside the current user's scope.");
        }

        var isUsed = _unitOfWork.Repository<CampaignMessage>().Query()
            .Any(message => message.TemplateId == template.Id);
        if (isUsed)
        {
            throw new InvalidOperationException("Template is used by a campaign and cannot be deleted.");
        }

        _unitOfWork.Repository<MessageTemplate>().Remove(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private bool CanAccess(MessageTemplate template)
    {
        return string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            template.ClientId is null ||
            template.ClientId == _currentUser.ClientId;
    }

    private static void Validate(string name, MessageType messageType, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException("Payload is required.");
        }

        if (!Enum.IsDefined(typeof(MessageType), messageType))
        {
            throw new ArgumentException("Invalid message type.");
        }
    }
}

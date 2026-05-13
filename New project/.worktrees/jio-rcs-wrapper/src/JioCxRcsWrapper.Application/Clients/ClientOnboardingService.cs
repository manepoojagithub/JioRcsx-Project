using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace JioCxRcsWrapper.Application.Clients;

public sealed class ClientOnboardingService : IClientOnboardingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuditService _auditService;

    public ClientOnboardingService(
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        IPasswordHasher<User> passwordHasher,
        IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
    }

    public async Task<int> CreateAsync(CreateClientRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        Validate(request);
        if (_unitOfWork.Repository<User>().Query().Any(x => x.Email == request.ManagerEmail))
        {
            throw new InvalidOperationException("Manager email already exists.");
        }

        var managerRole = _unitOfWork.Repository<Role>().Query().FirstOrDefault(x => x.Name == "Manager")
            ?? throw new InvalidOperationException("Manager role is not configured.");

        if (_unitOfWork.Repository<Client>().Query().Any(x => x.BrandName == request.BrandName.Trim()))
        {
            throw new InvalidOperationException("Brand name already exists.");
        }

        var client = new Client
        {
            BrandName = request.BrandName.Trim(),
            AgentName = request.AgentName.Trim(),
            AgentId = request.AgentId.Trim(),
            ApiKey = _secretProtector.Protect(request.ApiKey),
            LogoPath = request.LogoPath,
            SiteName = request.SiteName.Trim(),
            AgentUseCase = request.AgentUseCase,
            Credits = Math.Max(0, request.Credits),
            CreditCostPerMessage = Math.Max(1, request.CreditCostPerMessage),
            LowCreditThreshold = Math.Max(0, request.LowCreditThreshold),
            CreatedBy = adminUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<Client>().AddAsync(client, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var manager = new User
        {
            Name = request.ManagerName.Trim(),
            Email = request.ManagerEmail.Trim(),
            RoleId = managerRole.Id,
            ClientId = client.Id,
            IsActive = true,
            Credits = client.Credits,
            CreatedAt = DateTimeOffset.UtcNow
        };
        manager.PasswordHash = _passwordHasher.HashPassword(manager, request.ManagerPassword);
        manager.PlainTextPassword = request.ManagerPassword;

        await _unitOfWork.Repository<User>().AddAsync(manager, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(adminUserId, $"Created client {client.BrandName}", "Clients", cancellationToken);

        if (client.Credits > 0)
        {
            await _unitOfWork.Repository<UserCreditHistory>().AddAsync(new UserCreditHistory
            {
                UserId = manager.Id,
                Amount = client.Credits,
                PreviousBalance = 0,
                NewBalance = client.Credits,
                TransactionType = "Added",
                Reason = "Initial credits during onboarding",
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return client.Id;
    }

    public Task<IReadOnlyList<ClientSummary>> ListAsync(ClientFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<Client>().Query();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.BrandName))
                query = query.Where(x => x.BrandName.Contains(filter.BrandName));

            if (!string.IsNullOrWhiteSpace(filter.AgentName))
                query = query.Where(x => x.AgentName.Contains(filter.AgentName));

            if (!string.IsNullOrWhiteSpace(filter.AgentId))
                query = query.Where(x => x.AgentId.Contains(filter.AgentId));

            if (!string.IsNullOrWhiteSpace(filter.SiteName))
                query = query.Where(x => x.SiteName.Contains(filter.SiteName));
        }

        var clients = query
            .OrderBy(x => x.BrandName)
            .Select(x => new ClientSummary(x.Id, x.BrandName, x.AgentName, x.AgentId, x.SiteName, x.AgentUseCase, x.Credits, x.CreditCostPerMessage, x.LowCreditThreshold))
            .ToList();

        return Task.FromResult<IReadOnlyList<ClientSummary>>(clients);
    }

    public async Task<ClientDetails?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(id, cancellationToken);
        if (client is null) return null;

        var manager = _unitOfWork.Repository<User>().Query().FirstOrDefault(u => u.ClientId == client.Id);
        return new ClientDetails(client.Id, client.BrandName, client.AgentName, client.AgentId, client.SiteName, client.LogoPath, client.Credits, client.CreditCostPerMessage, client.LowCreditThreshold, manager?.Email, client.WebhookAuditEnabled, manager?.PlainTextPassword, client.ApiKey, client.AgentUseCase);
    }

    public async Task UpdateAsync(UpdateClientRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Client not found.");
        if (_unitOfWork.Repository<Client>().Query().Any(x => x.Id != request.Id && x.BrandName == request.BrandName.Trim()))
        {
            throw new InvalidOperationException("Brand name already exists.");
        }

        if (client.Credits != request.Credits)
        {
            var previousBalance = client.Credits;
            client.Credits = Math.Max(0, request.Credits);
            
            // Log history for all users belonging to this client so it shows up in everyone's panel
            var users = _unitOfWork.Repository<User>().Query().Where(u => u.ClientId == client.Id).ToArray();
            foreach (var user in users)
            {
                await _unitOfWork.Repository<UserCreditHistory>().AddAsync(new UserCreditHistory
                {
                    UserId = user.Id,
                    Amount = Math.Abs(client.Credits - previousBalance),
                    PreviousBalance = previousBalance,
                    NewBalance = client.Credits,
                    TransactionType = client.Credits > previousBalance ? "Added" : "Adjustment",
                    Reason = "Manual credit update by Administrator",
                    CreatedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
            }
        }

        client.BrandName = request.BrandName.Trim();
        client.AgentName = request.AgentName.Trim();
        if (!IsMaskedValue(request.AgentId))
        {
            client.AgentId = request.AgentId.Trim();
        }
        client.SiteName = request.SiteName.Trim();
        client.LogoPath = request.LogoPath;
        client.CreditCostPerMessage = Math.Max(1, request.CreditCostPerMessage);
        client.LowCreditThreshold = Math.Max(0, request.LowCreditThreshold);
        client.WebhookAuditEnabled = request.WebhookAuditEnabled;

        if (!string.IsNullOrWhiteSpace(request.ApiKey) && !IsMaskedValue(request.ApiKey))
        {
            client.ApiKey = _secretProtector.Protect(request.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(request.ManagerEmail))
        {
            var manager = _unitOfWork.Repository<User>().Query().FirstOrDefault(u => u.ClientId == client.Id);
            if (manager != null)
            {
                manager.Email = request.ManagerEmail.Trim();
                _unitOfWork.Repository<User>().Update(manager);
            }
        }

        foreach (var user in _unitOfWork.Repository<User>().Query().Where(user => user.ClientId == client.Id).ToArray())
        {
            user.Credits = client.Credits;
            _unitOfWork.Repository<User>().Update(user);
        }

        _unitOfWork.Repository<Client>().Update(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Client not found.");

        _unitOfWork.Repository<Client>().Remove(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(CreateClientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BrandName) ||
            string.IsNullOrWhiteSpace(request.AgentName) ||
            string.IsNullOrWhiteSpace(request.AgentId) ||
            string.IsNullOrWhiteSpace(request.ApiKey) ||
            string.IsNullOrWhiteSpace(request.SiteName) ||
            string.IsNullOrWhiteSpace(request.ManagerName) ||
            string.IsNullOrWhiteSpace(request.ManagerEmail) ||
            string.IsNullOrWhiteSpace(request.ManagerPassword) ||
            request.Credits < 0 ||
            request.CreditCostPerMessage <= 0 ||
            request.LowCreditThreshold < 0)
        {
            throw new ArgumentException("Client onboarding fields are required.");
        }
    }

    private static void Validate(UpdateClientRequest request)
    {
        if (request.Id <= 0 ||
            string.IsNullOrWhiteSpace(request.BrandName) ||
            string.IsNullOrWhiteSpace(request.AgentName) ||
            string.IsNullOrWhiteSpace(request.AgentId) ||
            string.IsNullOrWhiteSpace(request.SiteName) ||
            request.Credits < 0 ||
            request.CreditCostPerMessage <= 0 ||
            request.LowCreditThreshold < 0)
        {
            throw new ArgumentException("Client fields are required.");
        }
    }

    private static bool IsMaskedValue(string? value) => !string.IsNullOrWhiteSpace(value) && value.Contains('*');
}

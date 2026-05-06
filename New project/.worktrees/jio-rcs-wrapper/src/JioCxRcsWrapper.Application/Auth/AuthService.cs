using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace JioCxRcsWrapper.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher, IJwtTokenService jwtTokenService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        var user = _unitOfWork.Repository<User>()
            .Query()
            .FirstOrDefault(x => x.Email == normalizedEmail);

        if (user is null || !user.IsActive)
        {
            return Task.FromResult(Failed());
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Task.FromResult(Failed());
        }

        var roleEntity = user.Role ?? _unitOfWork.Repository<Role>().Query().FirstOrDefault(x => x.Id == user.RoleId);
        var role = roleEntity?.Name;
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(new LoginResult(false, "User role is not configured.", null, null, null, null, false, null));
        }

        var isDeveloper = roleEntity?.IsDeveloper == true;
        var token = _jwtTokenService.CreateToken(user.Id, user.Email, user.Name, role, user.ClientId, isDeveloper);
        return Task.FromResult(new LoginResult(true, null, user.Id, user.Name, role, user.ClientId, isDeveloper, token));
    }

    private static LoginResult Failed() =>
        new(false, "Invalid email or password.", null, null, null, null, false, null);
}

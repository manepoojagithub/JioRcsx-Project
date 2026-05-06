using JioCxRcsWrapper.Application.Clients;
using Microsoft.AspNetCore.DataProtection;

namespace JioCxRcsWrapper.Infrastructure.Security;

public sealed class ApiKeyProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public ApiKeyProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("JioCxRcsWrapper.ApiKeys.v1");
    }

    public string Protect(string value) => _protector.Protect(value);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

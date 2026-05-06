using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;

namespace JioCxRcsWrapper.Application.Clients;

public sealed class BrandingService : IBrandingService
{
    private readonly IUnitOfWork _unitOfWork;

    public BrandingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<BrandingResult> GetBrandingAsync(int? clientId, CancellationToken cancellationToken = default)
    {
        if (clientId.HasValue)
        {
            var client = _unitOfWork.Repository<Client>().Query().FirstOrDefault(x => x.Id == clientId.Value);
            if (client is not null)
            {
                return Task.FromResult(new BrandingResult(client.SiteName, client.LogoPath));
            }
        }

        var defaultBranding = _unitOfWork.Repository<ClientBrandingSetting>().Query().FirstOrDefault(x => x.IsDefault);
        return Task.FromResult(defaultBranding is null
            ? new BrandingResult("Advait Services", null)
            : new BrandingResult(defaultBranding.SiteName, defaultBranding.LogoPath));
    }
}

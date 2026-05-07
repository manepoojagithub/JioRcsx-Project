using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;

namespace JioCxRcsWrapper.Application.Security;

public sealed class UserCreditService : IUserCreditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UserCreditService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<UserCreditInfo?> GetCurrentUserCreditsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // We check client credits if it's a client user, otherwise maybe admin?
        // Requirement says "check credit client wise"
        if (_currentUser.ClientId.HasValue)
        {
            var client = await _unitOfWork.Repository<Client>().GetByIdAsync(_currentUser.ClientId.Value, cancellationToken);
            if (client is not null)
            {
                return new UserCreditInfo(client.Credits, client.LowCreditThreshold);
            }
        }
        else
        {
            // For admins or users without ClientId, check User.Credits
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(_currentUser.UserId, cancellationToken);
            if (user is not null)
            {
                return new UserCreditInfo(user.Credits, 10); // Default threshold 10 for users
            }
        }

        return null;
    }
}

using Cards.Web.Models;

namespace Cards.Web.Services;

public interface IUserService
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);

    Task<User> RegisterAsync(string username, string password, CancellationToken ct = default);

    Task<User?> AuthenticateAsync(string username, string password, CancellationToken ct = default);

    Task<int> GetTotalCountAsync(CancellationToken ct = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task SetPremiumAsync(Guid id, bool isPremium, CancellationToken ct = default);
}

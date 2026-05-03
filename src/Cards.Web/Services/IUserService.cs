using Cards.Web.Models;

namespace Cards.Web.Services;

public interface IUserService
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);

    Task<User> RegisterAsync(string username, string password, CancellationToken ct = default);

    Task<User?> AuthenticateAsync(string username, string password, CancellationToken ct = default);
}

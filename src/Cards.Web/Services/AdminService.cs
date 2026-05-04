using Microsoft.Extensions.Options;

namespace Cards.Web.Services;

public class AdminService : IAdminService
{
    private readonly IOptionsMonitor<AdminOptions> _options;

    public AdminService(IOptionsMonitor<AdminOptions> options)
    {
        _options = options;
    }

    public bool IsAdmin(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        var trimmed = username.Trim();
        return _options.CurrentValue.Usernames
            .Any(u => string.Equals(u, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}

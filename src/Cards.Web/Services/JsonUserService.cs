using System.Text.Json;
using Cards.Web.Models;

namespace Cards.Web.Services;

public class JsonUserService : IUserService
{
    private readonly string _dataDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonUserService(IWebHostEnvironment environment)
    {
        _dataDirectory = DataPathHelper.PrepareEntityPath(environment, "users");
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            return await FindByUsernameInternalAsync(username, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<User> RegisterAsync(string username, string password, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            var existing = await FindByUsernameInternalAsync(username, ct);
            if (existing is not null)
                throw new InvalidOperationException("A user with this name already exists.");

            var (hash, salt) = PasswordHasher.Hash(password);
            var user = new User
            {
                Username = username.Trim(),
                PasswordHash = hash,
                PasswordSalt = salt
            };

            await WriteAsync(user, ct);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            var user = await FindByUsernameInternalAsync(username, ct);
            if (user is null) return null;

            return PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt) ? user : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            return Directory.EnumerateFiles(_dataDirectory, "*.json").Count();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            var users = new List<User>();
            foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*.json"))
            {
                var user = await ReadFromFileAsync(file, ct);
                if (user is not null)
                    users.Add(user);
            }
            return users.OrderBy(u => u.CreatedAt).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            var path = Path.Combine(_dataDirectory, $"{id}.json");
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetPremiumAsync(Guid id, bool isPremium, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            var path = Path.Combine(_dataDirectory, $"{id}.json");
            if (!File.Exists(path))
                throw new InvalidOperationException($"User '{id}' not found.");

            var user = await ReadFromFileAsync(path, ct)
                ?? throw new InvalidOperationException($"User '{id}' could not be read.");

            if (user.IsPremium == isPremium)
                return;

            user.IsPremium = isPremium;
            await WriteAsync(user, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<User?> FindByUsernameInternalAsync(string username, CancellationToken ct)
    {
        var trimmed = username.Trim();
        foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*.json"))
        {
            var user = await ReadFromFileAsync(file, ct);
            if (user is not null && string.Equals(user.Username, trimmed, StringComparison.OrdinalIgnoreCase))
                return user;
        }
        return null;
    }

    private async Task<User?> ReadFromFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<User>(stream, _jsonOptions, ct);
    }

    private async Task WriteAsync(User user, CancellationToken ct)
    {
        var path = Path.Combine(_dataDirectory, $"{user.Id}.json");
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, user, _jsonOptions, ct);
        }

        if (File.Exists(path))
            File.Replace(tempPath, path, destinationBackupFileName: null);
        else
            File.Move(tempPath, path);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Cards.Web.Models;

namespace Cards.Web.Services;

public class JsonCollectionService : ICollectionService
{
    private readonly string _dataDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonCollectionService(IWebHostEnvironment environment)
    {
        _dataDirectory = Path.Combine(environment.ContentRootPath, "Data", "collections");
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(string userId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var collections = new List<Collection>();
            foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*.json"))
            {
                var item = await ReadFromFileAsync(file, ct);
                if (item is not null && item.UserId == userId)
                    collections.Add(item);
            }
            return collections.OrderByDescending(c => c.ModifiedAt).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var collections = new List<Collection>();
            foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*.json"))
            {
                var item = await ReadFromFileAsync(file, ct);
                if (item is not null)
                    collections.Add(item);
            }
            return collections.OrderByDescending(c => c.ModifiedAt).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await ReadAsync(id, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Collection> CreateAsync(string userId, string name, Language lang1, Language lang2, CancellationToken ct = default)
    {
        var (l1, l2) = LanguageHelper.NormalizePair(lang1, lang2);
        var collection = new Collection
        {
            UserId = userId,
            Name = name.Trim(),
            Language1 = l1,
            Language2 = l2
        };

        await _gate.WaitAsync(ct);
        try
        {
            await WriteAsync(collection, ct);
        }
        finally
        {
            _gate.Release();
        }
        return collection;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = GetFilePath(id);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddCardAsync(Guid collectionId, Guid cardId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var collection = await ReadAsync(collectionId, ct)
                ?? throw new InvalidOperationException($"Collection '{collectionId}' not found.");

            if (!collection.CardIds.Contains(cardId))
            {
                collection.CardIds.Add(cardId);
                collection.ModifiedAt = DateTime.UtcNow;
                await WriteAsync(collection, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveCardAsync(Guid collectionId, Guid cardId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var collection = await ReadAsync(collectionId, ct)
                ?? throw new InvalidOperationException($"Collection '{collectionId}' not found.");

            if (collection.CardIds.Remove(cardId))
            {
                collection.ModifiedAt = DateTime.UtcNow;
                await WriteAsync(collection, ct);
            }
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
            return Directory.EnumerateFiles(_dataDirectory, "*.json").Count();
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetFilePath(Guid id) => Path.Combine(_dataDirectory, $"{id}.json");

    private async Task<Collection?> ReadAsync(Guid id, CancellationToken ct)
    {
        var path = GetFilePath(id);
        return File.Exists(path) ? await ReadFromFileAsync(path, ct) : null;
    }

    private async Task<Collection?> ReadFromFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Collection>(stream, _jsonOptions, ct);
    }

    private async Task WriteAsync(Collection collection, CancellationToken ct)
    {
        var path = GetFilePath(collection.Id);
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, collection, _jsonOptions, ct);
        }

        if (File.Exists(path))
            File.Replace(tempPath, path, destinationBackupFileName: null);
        else
            File.Move(tempPath, path);
    }
}

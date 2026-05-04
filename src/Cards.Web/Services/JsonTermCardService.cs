using System.Text.Json;
using System.Text.Json.Serialization;
using Cards.Web.Models;

namespace Cards.Web.Services;

public class JsonTermCardService : ITermCardService
{
    private readonly string _dataDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonTermCardService(IWebHostEnvironment environment)
    {
        _dataDirectory = DataPathHelper.PrepareEntityPath(environment, "cards");
    }

    public async Task<TermCard?> GetByIdAsync(Guid id, CancellationToken ct = default)
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

    public async Task<List<TermCard>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cards = new List<TermCard>();
            foreach (var id in ids)
            {
                var card = await ReadAsync(id, ct);
                if (card is not null)
                    cards.Add(card);
            }
            return cards;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TermCard> CreateAsync(
        string userId,
        Language lang1,
        Language lang2,
        string text1,
        string text2,
        string? imageDataUrl,
        CancellationToken ct = default)
    {
        var (l1, l2) = LanguageHelper.NormalizePair(lang1, lang2);

        // Card-level image is stored on Value1; Value2 keeps no image
        var card = new TermCard
        {
            UserId = userId,
            Language1 = l1,
            Language2 = l2,
            Value1 = new TermValue
            {
                Text = text1.Trim(),
                ImageDataUrl = imageDataUrl,
                AudioStatus = AudioStatus.Pending
            },
            Value2 = new TermValue
            {
                Text = text2.Trim(),
                AudioStatus = AudioStatus.Pending
            }
        };

        await _gate.WaitAsync(ct);
        try
        {
            await WriteAsync(card, ct);
        }
        finally
        {
            _gate.Release();
        }
        return card;
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

    public async Task<IReadOnlyList<TermCard>> GetAllByUserAsync(string userId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cards = new List<TermCard>();
            foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*.json"))
            {
                var card = await ReadFromFileAsync(file, ct);
                if (card is not null && string.Equals(card.UserId, userId, StringComparison.Ordinal))
                    cards.Add(card);
            }
            return cards;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Guid>> GetAllIdsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var ids = new List<Guid>();
            foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (Guid.TryParse(name, out var id))
                    ids.Add(id);
            }
            return ids;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetFilePath(Guid id) => Path.Combine(_dataDirectory, $"{id}.json");

    private async Task<TermCard?> ReadAsync(Guid id, CancellationToken ct)
    {
        var path = GetFilePath(id);
        return File.Exists(path) ? await ReadFromFileAsync(path, ct) : null;
    }

    private async Task<TermCard?> ReadFromFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<TermCard>(stream, _jsonOptions, ct);
    }

    private async Task WriteAsync(TermCard card, CancellationToken ct)
    {
        var path = GetFilePath(card.Id);
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, card, _jsonOptions, ct);
        }

        if (File.Exists(path))
            File.Replace(tempPath, path, destinationBackupFileName: null);
        else
            File.Move(tempPath, path);
    }
}

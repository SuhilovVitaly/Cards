using System.Text.Json;
using System.Text.Json.Serialization;
using Cards.Web.Models;

namespace Cards.Web.Services;

public class JsonTermCardService : ITermCardService
{
    private readonly string _dataDirectory;
    private readonly string _audioDirectory;
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
        _audioDirectory = DataPathHelper.PrepareEntityPath(environment, "audio");
    }

    public async Task<TermCard?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
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
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
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
        string? audio1Base64 = null,
        CancellationToken ct = default)
    {
        // Card-level image and recorded audio are stored on Value1 (learning language).
        // Value2 (native language) keeps no image and no recording.
        var card = new TermCard
        {
            UserId = userId,
            Language1 = lang1,
            Language2 = lang2,
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

        var audioBytes = TryDecodeAudioDataUrl(audio1Base64);
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            string? audioPath = null;

            if (audioBytes is not null)
            {
                audioPath = Path.Combine(_audioDirectory, $"{card.Id}.webm");
                var tempAudioPath = audioPath + ".tmp";

                try
                {
                    await File.WriteAllBytesAsync(tempAudioPath, audioBytes, ct);
                    File.Move(tempAudioPath, audioPath, overwrite: true);
                    card.Value1.AudioPath = $"/audio/{card.Id}.webm";
                    card.Value1.AudioStatus = AudioStatus.Generated;
                }
                catch
                {
                    DeleteIfExists(tempAudioPath);
                    DeleteIfExists(audioPath);
                    throw;
                }
            }

            try
            {
                await WriteAsync(card, ct);
            }
            catch
            {
                if (audioPath is not null)
                    DeleteIfExists(audioPath);

                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
        return card;
    }

    private static byte[]? TryDecodeAudioDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;

        const string prefix = "data:";
        if (!dataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex < 0) return null;

        var meta = dataUrl.Substring(prefix.Length, commaIndex - prefix.Length);
        if (!meta.Contains("base64", StringComparison.OrdinalIgnoreCase)) return null;

        var payload = dataUrl[(commaIndex + 1)..];
        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task<TermCard?> UpdateAsync(
        Guid id,
        string text1,
        string text2,
        string? imageDataUrl,
        string? audio1Source,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            var card = await ReadAsync(id, ct);
            if (card is null) return null;

            card.Value1.Text = text1.Trim();
            card.Value2.Text = text2.Trim();

            // Image lives on Value1 by current convention; clear any legacy copy on Value2
            card.Value1.ImageDataUrl = imageDataUrl;
            card.Value2.ImageDataUrl = null;

            // audio1Source can be: null/empty (no audio), an existing path like "/audio/{id}.webm" (keep as is),
            // or a new "data:audio/...;base64,..." payload from a re-recording (overwrite the file).
            var audioBytes = TryDecodeAudioDataUrl(audio1Source);
            if (audioBytes is not null)
            {
                var audioPath = Path.Combine(_audioDirectory, $"{card.Id}.webm");
                await File.WriteAllBytesAsync(audioPath, audioBytes, ct);
                card.Value1.AudioPath = $"/audio/{card.Id}.webm";
                card.Value1.AudioStatus = AudioStatus.Generated;
            }

            card.ModifiedAt = DateTime.UtcNow;

            await WriteAsync(card, ct);
            return card;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TermCard?> UpdateLastViewedAsync(Guid id, DateTime viewedAt, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
            var card = await ReadAsync(id, ct);
            if (card is null) return null;

            card.LastViewedAt = viewedAt;

            await WriteAsync(card, ct);
            return card;
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
            var path = GetFilePath(id);
            if (File.Exists(path))
                File.Delete(path);

            var audioPath = Path.Combine(_audioDirectory, $"{id}.webm");
            if (File.Exists(audioPath))
                File.Delete(audioPath);
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

    public async Task<IReadOnlyList<TermCard>> GetAllByUserAsync(string userId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
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
            await using var dataLock = await DataFileLock.AcquireAsync(_dataDirectory, ct);
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

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

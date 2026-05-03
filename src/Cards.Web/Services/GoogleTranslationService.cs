using System.Text;
using System.Text.Json;
using Cards.Web.Models;

namespace Cards.Web.Services;

/// <summary>
/// Translates text via the public translate.googleapis.com endpoint.
/// No API key required, but the endpoint is unofficial and may break or rate-limit.
/// </summary>
public class GoogleTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleTranslationService> _logger;

    public GoogleTranslationService(HttpClient httpClient, ILogger<GoogleTranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> TranslateAsync(
        string text,
        Language sourceLanguage,
        Language targetLanguage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (sourceLanguage == targetLanguage) return text;

        var sourceCode = LanguageHelper.GetCode(sourceLanguage);
        var targetCode = LanguageHelper.GetCode(targetLanguage);

        var url =
            $"https://translate.googleapis.com/translate_a/single" +
            $"?client=gtx&sl={sourceCode}&tl={targetCode}&dt=t&q={Uri.EscapeDataString(text)}";

        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // Response shape: [[["translated","original",null,null,1], ...], ...]
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return null;

            var sentences = root[0];
            if (sentences.ValueKind != JsonValueKind.Array)
                return null;

            var sb = new StringBuilder();
            foreach (var sentence in sentences.EnumerateArray())
            {
                if (sentence.ValueKind == JsonValueKind.Array &&
                    sentence.GetArrayLength() > 0 &&
                    sentence[0].ValueKind == JsonValueKind.String)
                {
                    sb.Append(sentence[0].GetString());
                }
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Translation request failed");
            return null;
        }
    }
}

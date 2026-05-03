using Cards.Web.Models;

namespace Cards.Web.Services;

public interface ITranslationService
{
    Task<string?> TranslateAsync(
        string text,
        Language sourceLanguage,
        Language targetLanguage,
        CancellationToken ct = default);
}

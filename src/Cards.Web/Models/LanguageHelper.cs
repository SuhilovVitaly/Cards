namespace Cards.Web.Models;

public static class LanguageHelper
{
    public static string GetDisplayName(Language language) => language switch
    {
        Language.En => "English",
        Language.He => "Hebrew",
        Language.Ru => "Russian",
        _ => language.ToString()
    };

    public static string GetCode(Language language) => language switch
    {
        Language.En => "en",
        Language.He => "he",
        Language.Ru => "ru",
        _ => language.ToString().ToLowerInvariant()
    };

    public static string GetTextDirection(Language language) =>
        language == Language.He ? "rtl" : "ltr";

    public static (Language Lang1, Language Lang2) NormalizePair(Language a, Language b) =>
        a <= b ? (a, b) : (b, a);

    public static string GetPairDisplayName(Language lang1, Language lang2) =>
        $"{GetDisplayName(lang1)} — {GetDisplayName(lang2)}";

    public static string GetSpeechCode(Language language) => language switch
    {
        Language.En => "en-US",
        Language.He => "he-IL",
        Language.Ru => "ru-RU",
        _ => "en-US"
    };
}

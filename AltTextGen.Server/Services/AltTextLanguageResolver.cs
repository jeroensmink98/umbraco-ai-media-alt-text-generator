namespace AltTextGen.Server.Services;

internal static class AltTextLanguageResolver
{
    internal static string ResolveOutputLanguageCode(string? requestCulture, string configuredOutputLanguage)
    {
        if (!string.IsNullOrWhiteSpace(requestCulture))
        {
            string culture = requestCulture.Trim();
            int separatorIndex = culture.IndexOfAny(['-', '_']);
            string candidate = separatorIndex > 0
                ? culture[..separatorIndex]
                : culture;

            if (IsTwoLetterLanguageCode(candidate))
            {
                return candidate.ToLowerInvariant();
            }
        }

        return configuredOutputLanguage.Trim().ToLowerInvariant();
    }

    private static bool IsTwoLetterLanguageCode(string value)
    {
        return value.Length == 2
            && char.IsLetter(value[0])
            && char.IsLetter(value[1]);
    }
}

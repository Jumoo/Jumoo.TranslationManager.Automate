namespace Jumoo.TranslationManager.Automate.Triggers.Jobs;

/// <summary>Settings-based filtering shared by this package's job triggers.</summary>
internal static class TranslationJobTriggerFilter
{
    public static bool Matches(TranslationJobTriggerOutput output, TranslationJobTriggerSettings? settings)
    {
        if (settings is null) return true;

        if (settings.IgnoreErrors && output.IsError) return false;

        if (!MatchesCsv(settings.SetKeys, output.SetKey.ToString())) return false;
        if (!MatchesCsv(settings.ProviderKeys, output.ProviderKey.ToString())) return false;

        if (!string.IsNullOrWhiteSpace(settings.Cultures))
        {
            var cultures = settings.Cultures.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!cultures.Contains(output.TargetCulture, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool MatchesCsv(string? csv, string value)
    {
        if (string.IsNullOrWhiteSpace(csv)) return true;

        var values = csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return values.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}

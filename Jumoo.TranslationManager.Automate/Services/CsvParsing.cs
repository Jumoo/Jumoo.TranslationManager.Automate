namespace Jumoo.TranslationManager.Automate.Services;

internal static class CsvParsing
{
    public static string[]? ParseOrNull(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? null
            : csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static Guid[]? ParseGuidsOrNull(string? csv)
    {
        var values = ParseOrNull(csv);
        if (values is null) return null;

        var guids = new List<Guid>(values.Length);
        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var guid))
                guids.Add(guid);
        }

        return guids.Count == 0 ? null : guids.ToArray();
    }
}

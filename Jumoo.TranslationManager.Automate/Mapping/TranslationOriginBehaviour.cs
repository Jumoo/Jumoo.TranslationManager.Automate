using Umbraco.Automate.Core.Triggers;

namespace Jumoo.TranslationManager.Automate.Mapping;

/// <summary>
/// Shared dropdown editor and parsing for the "when triggered by another automation" field on
/// this package's trigger settings, mirroring uSync.Automate's equivalent origin-behaviour field.
/// </summary>
public static class TranslationOriginBehaviour
{
    public const string EditorUiAlias = "Umb.PropertyEditorUi.Dropdown";

    public const string EditorConfig = """
        [{ "alias": "items", "value": [
            { "name": "Always run", "value": "Run" },
            { "name": "Skip if this would loop", "value": "SkipOnCycle" },
            { "name": "Skip entirely", "value": "SkipAlways" }
        ] }]
        """;

    public static AutomationOriginatedEventBehavior Parse(string? value, AutomationOriginatedEventBehavior fallback)
        => Enum.TryParse<AutomationOriginatedEventBehavior>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}

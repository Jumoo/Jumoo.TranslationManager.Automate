using Jumoo.TranslationManager.Automate.Mapping;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;

namespace Jumoo.TranslationManager.Automate.Triggers.Jobs;

/// <summary>Settings shared by this package's job triggers.</summary>
public sealed class TranslationJobTriggerSettings : IAutomationOriginatedEventBehavior
{
    [Field(
        Label = "Translation Sets",
        Description = "Only fire for these translation sets (comma-separated set keys). Leave blank to match any set.",
        SortOrder = 0)]
    public string? SetKeys { get; set; }

    [Field(
        Label = "Target Cultures",
        Description = "Only fire for these target cultures (comma-separated, e.g. fr-FR, de-DE). Leave blank to match any culture.",
        SortOrder = 1)]
    public string? Cultures { get; set; }

    [Field(
        Label = "Connectors",
        Description = "Only fire for jobs sent to these translation connectors (comma-separated connector keys). Leave blank to match any connector.",
        SortOrder = 2)]
    public string? ProviderKeys { get; set; }

    [Field(
        Label = "Ignore failed jobs",
        Description = "Skip jobs that came back in an error state.",
        SortOrder = 3)]
    public bool IgnoreErrors { get; set; } = true;

    [Field(
        Label = "When triggered by another automation",
        Description = "How to handle translation jobs created or approved by another automation.",
        EditorUiAlias = TranslationOriginBehaviour.EditorUiAlias,
        EditorConfig = TranslationOriginBehaviour.EditorConfig,
        Group = "Advanced",
        SortOrder = 4)]
    public string OnAutomationOriginatedEvent { get; set; } = nameof(AutomationOriginatedEventBehavior.SkipOnCycle);

    AutomationOriginatedEventBehavior IAutomationOriginatedEventBehavior.OnAutomationOriginated
        => TranslationOriginBehaviour.Parse(OnAutomationOriginatedEvent, AutomationOriginatedEventBehavior.SkipOnCycle);
}

using Umbraco.Automate.Core.Settings;

namespace Jumoo.TranslationManager.Automate.Actions;

/// <summary>Settings for the <see cref="TranslateContentAction"/>.</summary>
public sealed class TranslateContentSettings
{
    [Field(
        Label = "Content",
        Description = "Key (GUID) of the content item to translate. Use ${trigger.contentKey} to translate the item that started this automation.",
        SupportsBindings = true,
        SortOrder = 0)]
    public string? ContentKey { get; set; }

    [Field(
        Label = "Target Cultures",
        Description = "Comma-separated culture codes (e.g. fr-FR, de-DE). Leave blank to translate to every culture in the matching translation sets.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string? Cultures { get; set; }

    [Field(
        Label = "Translation Sets",
        Description = "Comma-separated translation set keys (GUIDs) to restrict to. Leave blank to use every set that covers the content item.",
        SortOrder = 2)]
    public string? SetKeys { get; set; }

    [Field(
        Label = "Translation Connector",
        Description = "Key (GUID) of the translation connector to use. Leave blank to use the set's own connector, then the configured default.",
        SortOrder = 3)]
    public string? ProviderKey { get; set; }

    [Field(
        Label = "Job Name",
        Description = "Name for each created job. Leave blank for '{content} ({count}) to {culture}'.",
        SupportsBindings = true,
        SortOrder = 4)]
    public string? JobNameTemplate { get; set; }

    [Field(
        Label = "Include Children",
        Description = "Also translate every descendant of the content item.",
        SortOrder = 5,
        Group = "Advanced")]
    public bool IncludeDescendants { get; set; }

    [Field(
        Label = "Force Retranslate",
        Description = "Create translation nodes even where Translation Manager sees no change since the last translation.",
        SortOrder = 6,
        Group = "Advanced")]
    public bool Force { get; set; }

    [Field(
        Label = "Approve when returned",
        Description = "Approve the translation and write it back into Umbraco as soon as the connector returns it. Only use with connectors that return instantly (e.g. machine translation).",
        SortOrder = 7,
        Group = "Advanced")]
    public bool Approve { get; set; }

    [Field(
        Label = "Publish approved content",
        Description = "Publish the translated content after approving. Only applies when 'Approve when returned' is on.",
        SortOrder = 8,
        Group = "Advanced")]
    public bool Publish { get; set; }

    [Field(
        Label = "Max Job Details",
        Description = "Maximum number of created jobs to include in this step's output.",
        SortOrder = 9,
        Group = "Advanced")]
    public int MaxJobDetails { get; set; } = 20;
}

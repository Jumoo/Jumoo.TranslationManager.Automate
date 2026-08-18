using Umbraco.Automate.Core.Settings;

namespace Jumoo.TranslationManager.Automate.Actions;

/// <summary>Settings for the <see cref="ApproveTranslationJobAction"/>.</summary>
public sealed class ApproveTranslationJobSettings
{
    [Field(
        Label = "Translation Job",
        Description = "Key (GUID) of the translation job to approve. Use ${translate.jobKeys[0]} or a trigger's job key.",
        SupportsBindings = true,
        SortOrder = 0)]
    public string? JobKey { get; set; }

    [Field(
        Label = "Ask the connector first",
        Description = "Contact the translation connector for the latest translation before approving.",
        SortOrder = 1)]
    public bool Check { get; set; } = true;

    [Field(
        Label = "Publish approved content",
        Description = "Publish the translated content immediately after approving.",
        SortOrder = 2)]
    public bool Publish { get; set; } = true;

    [Field(
        Label = "Approve all nodes",
        Description = "Approve every node in the job. Turn off to approve only the nodes listed below.",
        SortOrder = 3,
        Group = "Advanced")]
    public bool ApproveAllNodes { get; set; } = true;

    [Field(
        Label = "Node Keys",
        Description = "Comma-separated translation node keys (GUIDs) to approve. Only used when 'Approve all nodes' is off.",
        SupportsBindings = true,
        SortOrder = 4,
        Group = "Advanced")]
    public string? NodeKeys { get; set; }
}

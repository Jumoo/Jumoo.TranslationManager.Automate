using Umbraco.Automate.Core.Settings;

namespace Jumoo.TranslationManager.Automate.Actions;

/// <summary>Settings for the <see cref="CheckTranslationJobAction"/>.</summary>
public sealed class CheckTranslationJobSettings
{
    [Field(
        Label = "Translation Job",
        Description = "Key (GUID) of the translation job to check. Use ${translate.jobKeys[0]} or a trigger's job key.",
        SupportsBindings = true,
        SortOrder = 0)]
    public string? JobKey { get; set; }

    [Field(
        Label = "Ask the connector",
        Description = "Contact the translation connector for the latest status. Turn off to read Translation Manager's stored status only.",
        SortOrder = 1)]
    public bool UpdateStatus { get; set; } = true;
}

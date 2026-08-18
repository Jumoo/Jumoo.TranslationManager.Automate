namespace Jumoo.TranslationManager.Automate.Configuration;

/// <summary>
/// Shared constants for actions, triggers and configuration in this package.
/// </summary>
public static class TranslationManagerAutomateConstants
{
    /// <summary>
    /// The alias of Translation Manager's backoffice section, used as <c>RequiredSections</c>
    /// on actions and triggers so Automate's dispatch/authorization gates match Translation
    /// Manager's own section. Verified by reflection against
    /// <c>Jumoo.TranslationManager.Core.Translate.SectionAlias</c> in Jumoo.TranslationManager.Core
    /// 17.7.1 - do not guess this value, it does not follow the lowercase convention used by
    /// Umbraco's own built-in sections (Content, Media, ...).
    /// </summary>
    public const string SectionAlias = "Umb.Section.Translation";

    /// <summary>Alias prefix shared by every action and trigger in this package.</summary>
    public const string AliasPrefix = "translationManager.";

    /// <summary>The Group shown for every action/trigger in the Automate step catalogue.</summary>
    public const string Group = "Translation Manager";
}

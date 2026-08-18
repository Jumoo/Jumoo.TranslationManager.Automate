namespace Jumoo.TranslationManager.Automate.Dispatch;

/// <summary>
/// Marker for this package's trigger outputs whose event subject is a translation set.
/// Automate's own <c>IContentScopedTriggerOutput</c> is internal to
/// <c>Umbraco.Automate.Core</c> and reserved for Automate's built-in content/media triggers -
/// its own remarks say provider packages should declare their own marker, which this is.
/// </summary>
public interface ITranslationSetScopedOutput
{
    Guid GetSetKey();
}

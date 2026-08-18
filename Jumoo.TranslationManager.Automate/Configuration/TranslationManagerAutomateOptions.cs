namespace Jumoo.TranslationManager.Automate.Configuration;

/// <summary>Global defaults for this package, bound from <c>TranslationManager:Automate</c>.</summary>
public class TranslationManagerAutomateOptions
{
    public const string Section = "TranslationManager:Automate";

    /// <summary>
    /// Translation connector used when neither the step settings nor the translation set
    /// names one.
    /// </summary>
    public Guid? DefaultProviderKey { get; set; }

    /// <summary>
    /// How long a translate/approve action waits for another translation run to finish
    /// before skipping. 0 = fail immediately.
    /// </summary>
    public int MaxWaitSeconds { get; set; }

    /// <summary>
    /// Cap on the ContentKeys array carried by a job trigger event. A bulk job can span
    /// thousands of nodes; the whole array is persisted in the outbox and the run row.
    /// </summary>
    public int MaxContentKeysPerEvent { get; set; } = 100;

    /// <summary>
    /// Suppress this package's own triggers while one of its actions is performing a
    /// translation write. On by default - an approve action re-firing the approved trigger
    /// into the same chain is never what anyone wants.
    /// </summary>
    public bool SuppressSelfOriginatedEvents { get; set; } = true;

    /// <summary>
    /// Allow falling back to the Umbraco super user when the workspace service account
    /// cannot be resolved to a backoffice user. Off by default: silently attributing
    /// translations to the super user hides who actually did the work.
    /// </summary>
    public bool AllowSuperUserFallback { get; set; }

    /// <summary>Translation sets these actions must never touch (set keys).</summary>
    public Guid[] ExcludedSets { get; set; } = [];
}

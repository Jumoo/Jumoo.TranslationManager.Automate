namespace Jumoo.TranslationManager.Automate.Services;

/// <summary>
/// Fail-fast in-process concurrency gate, guarding against two concurrent translation runs
/// creating duplicate open translation nodes for overlapping content - a second caller is
/// refused rather than queued, since a human would otherwise have to reconcile the duplicates.
/// Also doubles as the loop-protection signal: <see cref="IsRunning"/> lets this package's own
/// triggers suppress events raised by its own writes.
/// </summary>
public interface ITranslationOperationGate
{
    /// <summary>Attempts to acquire the gate, waiting up to <paramref name="wait"/>. Returns
    /// null if not acquired in time; dispose the result to release.</summary>
    Task<IDisposable?> TryAcquireAsync(TimeSpan wait, CancellationToken cancellationToken);

    /// <summary>True while a translation write is in progress on this node.</summary>
    bool IsRunning { get; }
}

namespace Jumoo.TranslationManager.Automate.Services;

/// <inheritdoc cref="ITranslationOperationGate" />
internal sealed class TranslationOperationGate : ITranslationOperationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool IsRunning => _semaphore.CurrentCount == 0;

    public async Task<IDisposable?> TryAcquireAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        var acquired = await _semaphore.WaitAsync(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, cancellationToken);
        return acquired ? new Lease(_semaphore) : null;
    }

    private sealed class Lease : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public Lease(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            _semaphore.Release();
        }
    }
}

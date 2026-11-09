namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Асинхронне блокування за ключем: при промаху до джерела даних іде лише один запит на ключ,
/// інші чекають і читають уже заповнений кеш (захист від «cache stampede»).
/// Семафор ключа видаляється, щойно його ніхто не використовує.
/// </summary>
internal sealed class KeyedLock
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken ct)
    {
        Entry entry;
        lock (_entries)
        {
            if (!_entries.TryGetValue(key, out entry!))
                _entries[key] = entry = new Entry();
            entry.References++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(ct);
        }
        catch
        {
            Release(key, entry, semaphoreTaken: false);
            throw;
        }

        return new Releaser(this, key, entry);
    }

    private void Release(string key, Entry entry, bool semaphoreTaken)
    {
        if (semaphoreTaken)
            entry.Semaphore.Release();

        lock (_entries)
        {
            if (--entry.References == 0)
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References { get; set; }
    }

    private sealed class Releaser(KeyedLock owner, string key, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Release(key, entry, semaphoreTaken: true);
        }
    }
}

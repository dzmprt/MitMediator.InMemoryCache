namespace MitMediator.InMemoryCache;

public class SemaphoreSlimWithUsers : IDisposable
{
    public string Name { get; private set; }

    public int UsersCount { get => _usersCount; }

    private int _usersCount;

    private SemaphoreSlim _semaphoreSlim;

    public SemaphoreSlimWithUsers(int initialCount, int maxCount, string name)
    {
        _semaphoreSlim = new SemaphoreSlim(initialCount, maxCount);
        Name = name;
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (Name)
        {
            waitTask = _semaphoreSlim.WaitAsync(cancellationToken);
            _usersCount++;
        }
        try
        {
            await waitTask;
        }
        catch
        {
            Interlocked.Decrement(ref _usersCount);
            throw;
        }
    }

    public void Release()
    {
        lock (Name)
        {
            _semaphoreSlim.Release();
            _usersCount--;
        }
    }

    public void Dispose()
    {
        _semaphoreSlim.Dispose();
    }
}
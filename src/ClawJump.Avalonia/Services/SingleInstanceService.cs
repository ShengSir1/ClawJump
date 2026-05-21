using System.Threading;

namespace ClawJump.Avalonia.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsFirstInstance { get; }

    public SingleInstanceService(string appName)
    {
        _mutex = new Mutex(
            initiallyOwned: true,
            name: appName,
            createdNew: out var createdNew);

        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        try
        {
            if (IsFirstInstance)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch
        {
            // 忽略释放异常，避免退出时报错
        }

        _mutex.Dispose();
    }
}
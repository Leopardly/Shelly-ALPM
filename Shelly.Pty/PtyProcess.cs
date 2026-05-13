using Shelly.Pty.Internal;
using Shelly.Pty.Native;

namespace Shelly.Pty;

public sealed class PtyProcess : IAsyncDisposable, IDisposable
{
    private readonly FileStream _master;
    private readonly PtyStream _stream;
    private readonly Task<int> _waitTask;
    private readonly SigwinchPump? _winchPump;
    private readonly Termios? _savedTermios;
    private int _disposed;

    internal PtyProcess(
        int pid,
        int masterFd,
        FileStream master,
        Task<int> waitTask,
        PtyHostMode mode,
        SigwinchPump? winchPump,
        Termios? savedTermios)
    {
        ProcessId = pid;
        MasterFd = masterFd;
        _master = master;
        _stream = new PtyStream(master);
        _waitTask = waitTask;
        Mode = mode;
        _winchPump = winchPump;
        _savedTermios = savedTermios;
    }

    public int ProcessId { get; }

    public PtyHostMode Mode { get; }

    internal int MasterFd { get; }

    public Stream Input => _stream;

    public Stream Output => _stream;

    public Task<int> WaitForExitAsync(CancellationToken ct = default)
        => ct.CanBeCanceled ? _waitTask.WaitAsync(ct) : _waitTask;

    public void Resize(ushort rows, ushort cols)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        PtyCore.Resize(MasterFd, rows, cols);
    }

    public void Kill(int signal = 15 )
    {
        if (_waitTask.IsCompleted) return;
        _ = Libc.kill(ProcessId, signal);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _winchPump?.Dispose();
        RestoreTermiosIfNeeded();
        _stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _winchPump?.Dispose();
        RestoreTermiosIfNeeded();
        await _stream.DisposeAsync().ConfigureAwait(false);
    }

    private void RestoreTermiosIfNeeded()
    {
        if (_savedTermios is not { } t) return;
        var copy = t;
        HostTty.RestoreTermios(ref copy);
    }
}

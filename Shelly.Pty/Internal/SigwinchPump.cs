using System.Runtime.InteropServices;

namespace Shelly.Pty.Internal;

internal sealed class SigwinchPump : IDisposable
{
    private readonly PosixSignalRegistration _registration;

    public SigwinchPump(Action<ushort, ushort> onResize)
    {

        const PosixSignal SIGWINCH = (PosixSignal)28;
        _registration = PosixSignalRegistration.Create(SIGWINCH, _ =>
        {
            if (HostTty.TryGetWinSize(out var rows, out var cols))
            {
                try { onResize(rows, cols); }
                catch {  }
            }
        });
    }

    public void Dispose() => _registration.Dispose();
}

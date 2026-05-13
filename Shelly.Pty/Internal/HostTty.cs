using Shelly.Pty.Native;

namespace Shelly.Pty.Internal;

internal static class HostTty
{
    public static bool IsHostTty()
        => Libc.isatty(Libc.STDIN_FILENO) == 1 && Libc.isatty(Libc.STDOUT_FILENO) == 1;

    public static bool TryGetWinSize(out ushort rows, out ushort cols)
    {
        var ws = default(WinSize);
        if (Libc.ioctl(Libc.STDOUT_FILENO, Libc.TIOCGWINSZ, ref ws) == 0 && ws.ws_row > 0 && ws.ws_col > 0)
        {
            rows = ws.ws_row;
            cols = ws.ws_col;
            return true;
        }
        rows = 0;
        cols = 0;
        return false;
    }

    public static bool TrySnapshotTermios(out Termios saved)
    {
        saved = default;
        return Libc.tcgetattr(Libc.STDIN_FILENO, ref saved) == 0;
    }

    public static void RestoreTermios(ref Termios t)
    {
        _ = Libc.tcsetattr(Libc.STDIN_FILENO, Libc.TCSAFLUSH, ref t);
    }

    public static bool TryEnterRawMode(out Termios previous)
    {
        if (!TrySnapshotTermios(out previous)) return false;

        var raw = previous;
        raw.c_iflag &= ~(Libc.IGNBRK | Libc.BRKINT | Libc.PARMRK | Libc.ISTRIP
                       | Libc.INLCR | Libc.IGNCR | Libc.ICRNL | Libc.IXON | Libc.INPCK);
        raw.c_oflag &= ~Libc.OPOST;
        raw.c_lflag &= ~(Libc.ECHO | Libc.ECHONL | Libc.ICANON | Libc.ISIG | Libc.IEXTEN);
        raw.c_cflag &= ~(Libc.CSIZE | Libc.PARENB);
        raw.c_cflag |= Libc.CS8;
        raw.c_cc[Libc.VMIN] = 1;
        raw.c_cc[Libc.VTIME] = 0;

        return Libc.tcsetattr(Libc.STDIN_FILENO, Libc.TCSAFLUSH, ref raw) == 0;
    }
}

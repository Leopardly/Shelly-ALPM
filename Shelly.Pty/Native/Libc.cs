using System.Runtime.InteropServices;

namespace Shelly.Pty.Native;

internal static partial class Libc
{
    private const string LibName = "libc";

    public const int O_RDWR = 0x0002;
    public const int O_NOCTTY = 0x0100;

    public const int F_GETFD = 1;
    public const int F_SETFD = 2;
    public const int FD_CLOEXEC = 1;

    public const ulong TIOCSCTTY = 0x540E;
    public const ulong TIOCGWINSZ = 0x5413;
    public const ulong TIOCSWINSZ = 0x5414;

    public const int TCSANOW = 0;
    public const int TCSADRAIN = 1;
    public const int TCSAFLUSH = 2;

    public const int VMIN = 6;
    public const int VTIME = 5;

    public const uint IGNBRK = 0x0001, BRKINT = 0x0002, PARMRK = 0x0008,
                      ISTRIP = 0x0020, INLCR = 0x0040, IGNCR = 0x0080,
                      ICRNL = 0x0100, IXON = 0x0400, INPCK = 0x0010;

    public const uint OPOST = 0x0001;

    public const uint ISIG = 0x0001, ICANON = 0x0002, ECHO = 0x0008,
                      ECHONL = 0x0040, IEXTEN = 0x8000;

    public const uint CSIZE = 0x0030, CS8 = 0x0030, PARENB = 0x0100;

    public const int SIGTERM = 15;
    public const int SIGKILL = 9;

    public const int STDIN_FILENO = 0;
    public const int STDOUT_FILENO = 1;
    public const int STDERR_FILENO = 2;

    public const int EIO = 5;
    public const int EINTR = 4;

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int posix_openpt(int flags);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int grantpt(int fd);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int unlockpt(int fd);

    [LibraryImport(LibName, SetLastError = true)]
    public static unsafe partial int ptsname_r(int fd, byte* buf, nuint buflen);

    [LibraryImport(LibName, SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int open(string path, int flags);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int close(int fd);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int dup2(int oldfd, int newfd);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int fcntl(int fd, int cmd, int arg);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int ioctl(int fd, ulong request, ref WinSize ws);

    [LibraryImport(LibName, SetLastError = true, EntryPoint = "ioctl")]
    public static partial int ioctl_ptr(int fd, ulong request, nint arg);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int tcgetattr(int fd, ref Termios t);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int tcsetattr(int fd, int optional_actions, ref Termios t);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int isatty(int fd);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int fork();

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int setsid();

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int chdir([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int kill(int pid, int sig);

    [LibraryImport(LibName, SetLastError = true)]
    public static partial int waitpid(int pid, out int wstatus, int options);

    [LibraryImport(LibName, EntryPoint = "_exit")]
    public static partial void _exit(int status);

    [LibraryImport(LibName, SetLastError = true, EntryPoint = "execve")]
    public static unsafe partial int execve(byte* path, byte** argv, byte** envp);

    [LibraryImport(LibName, SetLastError = true, EntryPoint = "execvp")]
    public static unsafe partial int execvp(byte* file, byte** argv);

    public static bool WIFEXITED(int status) => (status & 0x7f) == 0;
    public static int WEXITSTATUS(int status) => (status >> 8) & 0xff;
    public static bool WIFSIGNALED(int status) => ((status & 0x7f) + 1) >> 1 > 0 && !WIFEXITED(status);
    public static int WTERMSIG(int status) => status & 0x7f;
}

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Shelly.Pty.Native;

namespace Shelly.Pty.Internal;

internal static class PtyCore
{
    public readonly record struct StartResult(int MasterFd, int Pid);

    public static unsafe StartResult OpenAndFork(PtyOptions options, IReadOnlyDictionary<string, string> env)
    {
        int master = Libc.posix_openpt(Libc.O_RDWR | Libc.O_NOCTTY);
        if (master < 0) throw Errno("posix_openpt failed");

        byte* slavePathPtr = null;
        byte* cwdPtr = null;
        byte** argvPtr = null;
        byte** envpPtr = null;
        var allocations = new List<nint>(capacity: 16);

        try
        {
            if (options.CloseOnExec)
            {
                var flags = Libc.fcntl(master, Libc.F_GETFD, 0);
                if (flags >= 0) Libc.fcntl(master, Libc.F_SETFD, flags | Libc.FD_CLOEXEC);
            }

            if (Libc.grantpt(master) != 0) throw Errno("grantpt failed");
            if (Libc.unlockpt(master) != 0) throw Errno("unlockpt failed");

            string slavePath = GetSlavePath(master);

            var ws = new WinSize { ws_row = options.Rows, ws_col = options.Cols };
            _ = Libc.ioctl(master, Libc.TIOCSWINSZ, ref ws);

            slavePathPtr = AllocUtf8(slavePath, allocations);
            if (!string.IsNullOrEmpty(options.WorkingDirectory))
                cwdPtr = AllocUtf8(options.WorkingDirectory!, allocations);

            int argc = 1 + options.Args.Length;
            argvPtr = AllocPtrArray(argc + 1, allocations);
            argvPtr[0] = AllocUtf8(options.Command, allocations);
            for (int i = 0; i < options.Args.Length; i++)
                argvPtr[1 + i] = AllocUtf8(options.Args[i], allocations);
            argvPtr[argc] = null;

            envpPtr = AllocPtrArray(env.Count + 1, allocations);
            int ei = 0;
            foreach (var kv in env)
            {
                envpPtr[ei++] = AllocUtf8($"{kv.Key}={kv.Value}", allocations);
            }
            envpPtr[ei] = null;

            int pid = Libc.fork();
            if (pid < 0) throw Errno("fork failed");

            if (pid == 0)
            {

                ChildSetupAndExec(master, slavePathPtr, cwdPtr, argvPtr, envpPtr);
                Libc._exit(127);
            }

            FreeAll(allocations);
            return new StartResult(master, pid);
        }
        catch
        {
            FreeAll(allocations);
            Libc.close(master);
            throw;
        }
    }

    private static unsafe void ChildSetupAndExec(
        int masterFd,
        byte* slavePathPtr,
        byte* cwdPtr,
        byte** argv,
        byte** envp)
    {
        if (Libc.setsid() < 0) Libc._exit(127);

        int slaveFd = OpenSlave(slavePathPtr);
        if (slaveFd < 0) Libc._exit(127);

        _ = Libc.ioctl_ptr(slaveFd, Libc.TIOCSCTTY, 0);

        if (Libc.dup2(slaveFd, Libc.STDIN_FILENO) < 0) Libc._exit(127);
        if (Libc.dup2(slaveFd, Libc.STDOUT_FILENO) < 0) Libc._exit(127);
        if (Libc.dup2(slaveFd, Libc.STDERR_FILENO) < 0) Libc._exit(127);

        if (slaveFd > 2) Libc.close(slaveFd);
        Libc.close(masterFd);

        if (cwdPtr != null)
        {
            if (ChdirNative(cwdPtr) != 0) Libc._exit(127);
        }

        Libc.execve(argv[0], argv, envp);
        Libc._exit(127);
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern unsafe int open_raw(byte* path, int flags);

    [DllImport("libc", EntryPoint = "chdir", SetLastError = true)]
    private static extern unsafe int chdir_raw(byte* path);

    private static unsafe int OpenSlave(byte* path) => open_raw(path, Libc.O_RDWR);
    private static unsafe int ChdirNative(byte* path) => chdir_raw(path);

    private static unsafe byte* AllocUtf8(string s, List<nint> allocations)
    {
        int byteCount = Encoding.UTF8.GetByteCount(s);
        byte* p = (byte*)NativeMemory.Alloc((nuint)(byteCount + 1));
        allocations.Add((nint)p);
        var span = new Span<byte>(p, byteCount + 1);
        Encoding.UTF8.GetBytes(s, span);
        span[byteCount] = 0;
        return p;
    }

    private static unsafe byte** AllocPtrArray(int count, List<nint> allocations)
    {
        byte** p = (byte**)NativeMemory.AllocZeroed((nuint)count, (nuint)sizeof(nint));
        allocations.Add((nint)p);
        return p;
    }

    private static unsafe void FreeAll(List<nint> allocations)
    {
        foreach (var p in allocations)
            NativeMemory.Free((void*)p);
        allocations.Clear();
    }

    private static unsafe string GetSlavePath(int master)
    {
        Span<byte> buf = stackalloc byte[256];
        fixed (byte* p = buf)
        {
            if (Libc.ptsname_r(master, p, (nuint)buf.Length) != 0)
                throw Errno("ptsname_r failed");
        }
        int len = buf.IndexOf((byte)0);
        return Encoding.UTF8.GetString(buf[..(len < 0 ? buf.Length : len)]);
    }

    public static FileStream WrapMaster(int masterFd)
    {
        var safe = new SafeFileHandle((nint)masterFd, ownsHandle: true);

        return new FileStream(safe, FileAccess.ReadWrite, bufferSize: 4096, isAsync: false);
    }

    public static void Resize(int masterFd, ushort rows, ushort cols)
    {
        var ws = new WinSize { ws_row = rows, ws_col = cols };
        if (Libc.ioctl(masterFd, Libc.TIOCSWINSZ, ref ws) != 0)
            throw Errno("TIOCSWINSZ failed");
    }

    public static Task<int> WaitAsync(int pid)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var t = new Thread(() =>
        {
            int status;
            while (true)
            {
                int r = Libc.waitpid(pid, out status, 0);
                if (r == pid) break;
                int err = Marshal.GetLastPInvokeError();
                if (err == Libc.EINTR) continue;
                tcs.TrySetException(new PtyException("waitpid failed", err));
                return;
            }

            int exitCode;
            if (Libc.WIFEXITED(status)) exitCode = Libc.WEXITSTATUS(status);
            else if (Libc.WIFSIGNALED(status)) exitCode = 128 + Libc.WTERMSIG(status);
            else exitCode = -1;
            tcs.TrySetResult(exitCode);
        })
        {
            IsBackground = true,
            Name = $"Shelly.Pty waitpid({pid})",
        };
        t.Start();
        return tcs.Task;
    }

    private static PtyException Errno(string message)
        => new(message, Marshal.GetLastPInvokeError());
}

using System.Runtime.CompilerServices;
using Shelly.Pty.Internal;
using Shelly.Pty.Native;

namespace Shelly.Pty;

public static class PtyFactory
{
    public static PtyProcess Start(PtyOptions options) => options.HostMode switch
    {
        PtyHostMode.Headless => StartHeadless(options),
        PtyHostMode.Interactive => StartInteractive(options),
        _ => throw new ArgumentOutOfRangeException(nameof(options), options.HostMode, "Unknown PtyHostMode"),
    };

    public static PtyProcess StartHeadless(PtyOptions options)
    {
        EnsureLinux();
        var env = BuildEnv(options, inheritHost: false);
        var result = PtyCore.OpenAndFork(options, env);
        var master = PtyCore.WrapMaster(result.MasterFd);
        var wait = PtyCore.WaitAsync(result.Pid);
        return new PtyProcess(result.Pid, result.MasterFd, master, wait, PtyHostMode.Headless, null, null);
    }

    public static PtyProcess StartInteractive(PtyOptions options)
    {
        EnsureLinux();
        if (!HostTty.IsHostTty())
            throw new PtyException("Interactive mode requires the host process to be attached to a TTY.");

        var effective = options;
        if (options.Rows == 24 && options.Cols == 80 && HostTty.TryGetWinSize(out var rows, out var cols))
        {
            effective = new PtyOptions
            {
                Command = options.Command,
                Args = options.Args,
                WorkingDirectory = options.WorkingDirectory,
                Environment = options.Environment,
                Rows = rows,
                Cols = cols,
                HostMode = options.HostMode,
                UseDefaultEnvironment = options.UseDefaultEnvironment,
                CloseOnExec = options.CloseOnExec,
                InheritHostEnvironment = options.InheritHostEnvironment,
                AutoForwardWindowSize = options.AutoForwardWindowSize,
                SetHostTerminalRaw = options.SetHostTerminalRaw,
            };
        }

        var env = BuildEnv(effective, inheritHost: effective.InheritHostEnvironment);
        var result = PtyCore.OpenAndFork(effective, env);
        var master = PtyCore.WrapMaster(result.MasterFd);
        var wait = PtyCore.WaitAsync(result.Pid);

        Termios? saved = null;
        if (effective.SetHostTerminalRaw && HostTty.TryEnterRawMode(out var prev))
            saved = prev;

        SigwinchPump? pump = null;
        if (effective.AutoForwardWindowSize)
        {
            int masterFd = result.MasterFd;
            pump = new SigwinchPump((r, c) =>
            {
                try { PtyCore.Resize(masterFd, r, c); } catch {  }
            });
        }

        return new PtyProcess(result.Pid, result.MasterFd, master, wait, PtyHostMode.Interactive, pump, saved);
    }

    private static void EnsureLinux()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Shelly.Pty supports Linux only.");
        if (RuntimeFeature.IsDynamicCodeSupported)
            throw new PlatformNotSupportedException(
                "Shelly.Pty is AOT-only. Publish the host with PublishAot=true.");
    }

    private static IReadOnlyDictionary<string, string> BuildEnv(PtyOptions options, bool inheritHost)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);

        if (options.UseDefaultEnvironment)
        {
            env["TERM"] = "xterm-256color";
            env["PATH"] = "/usr/local/bin:/usr/bin:/bin";
            env["LANG"] = "C.UTF-8";
        }

        if (inheritHost)
        {
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string k && entry.Value is string v)
                    env[k] = v;
            }
        }

        if (options.Environment is { } overrides)
        {
            foreach (var kv in overrides)
                env[kv.Key] = kv.Value;
        }

        return env;
    }
}

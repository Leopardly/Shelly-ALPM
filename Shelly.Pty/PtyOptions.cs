namespace Shelly.Pty;

public enum PtyHostMode
{

    Headless,

    Interactive,
}

public sealed class PtyOptions
{

    public required string Command { get; init; }

    public string[] Args { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    public ushort Rows { get; init; } = 24;

    public ushort Cols { get; init; } = 80;

    public PtyHostMode HostMode { get; init; } = PtyHostMode.Headless;

    public bool UseDefaultEnvironment { get; init; } = true;

    public bool CloseOnExec { get; init; } = true;

    public bool InheritHostEnvironment { get; init; } = true;

    public bool AutoForwardWindowSize { get; init; } = true;

    public bool SetHostTerminalRaw { get; init; } = true;
}

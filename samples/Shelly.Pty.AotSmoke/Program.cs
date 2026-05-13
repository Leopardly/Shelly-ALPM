using System.Text;
using Shelly.Pty;

int failures = 0;

await RunAsync("echo round-trip", EchoRoundTripAsync);
await RunAsync("kill -> SIGTERM exit", KillAsync);
await RunAsync("resize ioctl", ResizeAsync);
await RunAsync("interactive guard / cleanup", InteractiveGuardAsync);

Console.WriteLine(failures == 0 ? "ALL OK" : $"FAILED: {failures}");
return failures;

async Task RunAsync(string name, Func<Task> body)
{
    try { await body(); Console.WriteLine($"[ OK ] {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"[FAIL] {name}: {ex.Message}"); }
}

static async Task EchoRoundTripAsync()
{
    await using var pty = PtyFactory.Start(new PtyOptions
    {
        Command = "/bin/sh",
        Args = ["-c", "echo hello-from-pty"],
    });

    var sb = new StringBuilder();
    var buf = new byte[4096];
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var readTask = Task.Run(async () =>
    {
        try
        {
            int n;
            while ((n = await pty.Output.ReadAsync(buf, cts.Token)) > 0)
                sb.Append(Encoding.UTF8.GetString(buf, 0, n));
        }
        catch (OperationCanceledException) { }
    });

    int code = await pty.WaitForExitAsync(cts.Token);
    await readTask;
    if (code != 0) throw new Exception($"exit={code}");
    if (!sb.ToString().Contains("hello-from-pty")) throw new Exception($"output missing: {sb}");
}

static async Task KillAsync()
{
    await using var pty = PtyFactory.Start(new PtyOptions
    {
        Command = "/bin/sh",
        Args = ["-c", "sleep 30"],
    });
    pty.Kill();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    int code = await pty.WaitForExitAsync(cts.Token);
    if (code == 0) throw new Exception("expected non-zero exit");
}

static async Task ResizeAsync()
{
    await using var pty = PtyFactory.Start(new PtyOptions
    {
        Command = "/bin/sh",
        Args = ["-c", "sleep 1"],
    });
    pty.Resize(40, 100);
    pty.Kill();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    _ = await pty.WaitForExitAsync(cts.Token);
}

static async Task InteractiveGuardAsync()
{

    try
    {
        await using var pty = PtyFactory.StartInteractive(new PtyOptions
        {
            Command = "/bin/sh",
            Args = ["-c", "true"],
            SetHostTerminalRaw = false,
            AutoForwardWindowSize = false,
        });

        pty.Kill();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = await pty.WaitForExitAsync(cts.Token);
    }
    catch (PtyException) {  }
}

namespace Shelly.Pty;

public static class PtyConsoleBridge
{
    public static async Task<int> RunAsync(PtyProcess pty, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pty);

        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = linkedCts.Token;

        var pumpIn = Task.Run(async () =>
        {
            try
            {
                var buf = new byte[4096];
                int n;
                while (!token.IsCancellationRequested && (n = await stdin.ReadAsync(buf.AsMemory(), token).ConfigureAwait(false)) > 0)
                {
                    await pty.Input.WriteAsync(buf.AsMemory(0, n), token).ConfigureAwait(false);
                    await pty.Input.FlushAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }, token);

        var pumpOut = Task.Run(async () =>
        {
            try
            {
                var buf = new byte[4096];
                int n;
                while ((n = await pty.Output.ReadAsync(buf.AsMemory(), token).ConfigureAwait(false)) > 0)
                {
                    await stdout.WriteAsync(buf.AsMemory(0, n), token).ConfigureAwait(false);
                    await stdout.FlushAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }, token);

        int exit = await pty.WaitForExitAsync(token).ConfigureAwait(false);
        linkedCts.Cancel();
        await Task.WhenAny(Task.WhenAll(pumpIn, pumpOut), Task.Delay(250, CancellationToken.None)).ConfigureAwait(false);
        return exit;
    }
}

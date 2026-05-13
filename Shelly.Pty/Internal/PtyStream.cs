using System.Runtime.InteropServices;
using Shelly.Pty.Native;

namespace Shelly.Pty.Internal;

internal sealed class PtyStream : Stream
{
    private readonly Stream _inner;

    public PtyStream(Stream inner)
    {
        _inner = inner;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanWrite => _inner.CanWrite;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        try { return _inner.Read(buffer, offset, count); }
        catch (IOException ex) when (IsEio(ex)) { return 0; }
    }

    public override int Read(Span<byte> buffer)
    {
        try { return _inner.Read(buffer); }
        catch (IOException ex) when (IsEio(ex)) { return 0; }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        try { return await _inner.ReadAsync(buffer.AsMemory(offset, count), ct).ConfigureAwait(false); }
        catch (IOException ex) when (IsEio(ex)) { return 0; }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        try { return await _inner.ReadAsync(buffer, ct).ConfigureAwait(false); }
        catch (IOException ex) when (IsEio(ex)) { return 0; }
    }

    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => _inner.WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        => _inner.WriteAsync(buffer, ct);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync() => _inner.DisposeAsync();

    private static bool IsEio(IOException ex)
    {

        var errno = ex.HResult & 0xffff;
        return errno == Libc.EIO;
    }
}

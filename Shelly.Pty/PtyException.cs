namespace Shelly.Pty;

public sealed class PtyException : Exception
{
    public int Errno { get; }

    public PtyException(string message) : base(message) { }

    public PtyException(string message, int errno)
        : base($"{message} (errno={errno})")
    {
        Errno = errno;
    }
}

using System.Runtime.InteropServices;

namespace Shelly.Pty.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct WinSize
{
    public ushort ws_row;
    public ushort ws_col;
    public ushort ws_xpixel;
    public ushort ws_ypixel;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Termios
{
    public uint c_iflag;
    public uint c_oflag;
    public uint c_cflag;
    public uint c_lflag;
    public byte c_line;
    public CcArray c_cc;
    public uint c_ispeed;
    public uint c_ospeed;
}

[System.Runtime.CompilerServices.InlineArray(32)]
internal struct CcArray
{
    private byte _element0;
}

using System.Runtime.InteropServices;
using LumeneraViewer.Camera;

namespace LumeneraViewer.Tests;

public class LuCamInteropTests
{
    [Fact]
    public void FrameFormat_Is28Bytes()
    {
        // lucam.h: 5×ULONG + 4×USHORT = 28 байт
        Assert.Equal(28, Marshal.SizeOf<LucamFrameFormat>());
    }
}

using System.Runtime.InteropServices;

namespace LumeneraViewer.Camera;

// P/Invoke к lucamapi.dll (x64). Сигнатуры — спецификация §4, восстановлены по публичному SDK.
internal static class NativeMethods
{
    internal const string Dll = "lucamapi.dll";

    // LucamStreamVideoControl
    internal const uint StopStreaming = 0;
    internal const uint StartStreaming = 1;

    // pixelFormat
    internal const uint PF_8 = 0;
    internal const uint PF_16 = 1;

    // property
    internal const uint PropExposure = 20;
    internal const uint PropGain = 40;

    // Колбэк потока: void __stdcall cb(void* ctx, BYTE* data, ULONG len)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void StreamingCallback(nint context, nint data, uint length);

    [DllImport(Dll)] internal static extern int LucamNumCameras();
    [DllImport(Dll)] internal static extern nint LucamCameraOpen(uint index);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamCameraClose(nint h);
    [DllImport(Dll)] internal static extern uint LucamGetLastError();
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamGetFormat(nint h, ref LucamFrameFormat format, out float frameRate);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamSetFormat(nint h, ref LucamFrameFormat format, float frameRate);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamStreamVideoControl(nint h, uint ctrl, nint hwnd);
    [DllImport(Dll)] internal static extern int LucamAddStreamingCallback(nint h, StreamingCallback cb, nint context);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamRemoveStreamingCallback(nint h, int id);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamConvertFrameToRgb24(nint h, nint dest, nint src, uint width, uint height, uint pixelFormat, ref LucamConversion conversion);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamGetProperty(nint h, uint prop, out float value, out int flags);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamSetProperty(nint h, uint prop, float value, int flags);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamPropertyRange(nint h, uint prop, out float min, out float max, out float def, out int flags);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LucamQueryVersion(nint h, out LucamVersion version);
    /// <summary>Доступные частоты кадров текущего формата; entryCount = 0 и rates = null — только количество.</summary>
    [DllImport(Dll)] internal static extern int LucamEnumAvailableFrameRates(nint h, uint entryCount, [Out] float[]? rates);
}

/// <summary>LUCAM_FRAME_FORMAT, 28 байт.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LucamFrameFormat
{
    public uint XOffset;
    public uint YOffset;
    public uint Width;
    public uint Height;
    public uint PixelFormat;
    public ushort SubSampleX;   // union с binningX
    public ushort FlagsX;
    public ushort SubSampleY;   // union с binningY
    public ushort FlagsY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LucamConversion
{
    public uint DemosaicMethod;     // HIGH_QUALITY = 2
    public uint CorrectionMatrix;   // NONE = 0
}

[StructLayout(LayoutKind.Sequential)]
internal struct LucamVersion
{
    public uint Firmware, Fpga, Api, Driver, SerialNumber, Reserved;
}

using System.Runtime.InteropServices;

namespace LumeneraViewer.Camera;

// Сигнатуры основаны на публично задокументированном LuCam API (Lumenera/Teledyne Lumenera SDK),
// который стабилен на протяжении многих версий SDK. Перед использованием сверьте с реальным
// lucam.h из установленного SDK (обычно: C:\Program Files\Lumenera Corporation\LuCam Software\SDK\Inc\lucam.h) —
// особенно порядок и размеры полей LucamFrameFormat и калинг-конвенцию делегата стрим-коллбэка.
internal static class NativeMethods
{
    private const string LuCamDll = "lucam.dll";

    internal const uint StreamControlStartStreaming = 1;
    internal const uint StreamControlStopStreaming = 2;

    [DllImport(LuCamDll, EntryPoint = "LucamNumCameras")]
    internal static extern int LucamNumCameras();

    [DllImport(LuCamDll, EntryPoint = "LucamCameraOpen")]
    internal static extern nint LucamCameraOpen(uint cameraIndex);

    [DllImport(LuCamDll, EntryPoint = "LucamCameraClose")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamCameraClose(nint hCamera);

    [DllImport(LuCamDll, EntryPoint = "LucamGetLastError")]
    internal static extern uint LucamGetLastError();

    [DllImport(LuCamDll, EntryPoint = "LucamGetFormat")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamGetFormat(nint hCamera, ref LucamFrameFormat frameFormat, ref float frameRate);

    [DllImport(LuCamDll, EntryPoint = "LucamSetFormat")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamSetFormat(nint hCamera, ref LucamFrameFormat frameFormat, float frameRate);

    [DllImport(LuCamDll, EntryPoint = "LucamStreamVideoControl")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamStreamVideoControl(nint hCamera, uint controlType, nint hWnd);

    // Делегат держим живым полем в LuCamCamera, пока идёт стрим — иначе GC может собрать
    // его, а нативный код продолжит вызывать по уже недействительному указателю.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void LucamVideoFrameCallback(nint context, nint pData, int dataLength);

    [DllImport(LuCamDll, EntryPoint = "LucamAddStreamingCallback")]
    internal static extern uint LucamAddStreamingCallback(nint hCamera, LucamVideoFrameCallback callback, nint context);

    [DllImport(LuCamDll, EntryPoint = "LucamRemoveStreamingCallback")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamRemoveStreamingCallback(nint hCamera, uint callbackId);

    // Конвертирует сырые данные с сенсора (в т.ч. Bayer-мозаику) в RGB24 — избавляет нас
    // от необходимости самим знать точные числовые коды pixelFormat из lucam.h.
    [DllImport(LuCamDll, EntryPoint = "LucamConvertFrameToRgb24")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LucamConvertFrameToRgb24(
        nint hCamera,
        nint rgbBuffer,
        nint rawData,
        uint width,
        uint height,
        uint pixelFormat,
        uint imageBitDepth,
        [MarshalAs(UnmanagedType.Bool)] bool flipX,
        [MarshalAs(UnmanagedType.Bool)] bool flipY);
}

[StructLayout(LayoutKind.Sequential)]
internal struct LucamFrameFormat
{
    public uint XOffset;
    public uint YOffset;
    public uint Width;
    public uint Height;
    public byte BinningX;
    public byte FlagsX;
    public byte BinningY;
    public byte FlagsY;
    public uint PixelFormat;
    public ushort SubSampleX;
    public ushort SubSampleY;
    public ushort Reserved0;
    public ushort Reserved1;
}

using System.Runtime.InteropServices;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Camera;

public static class CameraFactory
{
    /// <summary>Драйвер есть, если lucamapi.dll загружается. P/Invoke не вызывается.</summary>
    public static bool IsDriverAvailable()
    {
        if (!NativeLibrary.TryLoad(NativeMethods.Dll, out var h)) return false;
        NativeLibrary.Free(h);
        return true;
    }

    /// <summary>Число подключённых камер; 0 без драйвера или при ошибке.</summary>
    public static int CountRealCameras()
    {
        if (!IsDriverAvailable()) return 0;
        try { return Math.Max(0, NativeMethods.LucamNumCameras()); }
        catch { return 0; }
    }

    public static ICamera CreateReal(CameraSettings s) => new LuCamCamera(s);

    public static ICamera CreateSimulated() => new SimulatedCamera();
    public static ICamera CreateSimulated(int width, int height) => new SimulatedCamera(width, height);
}

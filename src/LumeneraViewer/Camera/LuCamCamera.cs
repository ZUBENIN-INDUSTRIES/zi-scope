using System.Runtime.InteropServices;

namespace LumeneraViewer.Camera;

public sealed class LuCamCamera : IDisposable
{
    private const uint ImageBitDepth = 8;

    private nint _handle;
    private uint _callbackId;
    private NativeMethods.LucamVideoFrameCallback? _callback;
    private int _width;
    private int _height;
    private uint _pixelFormat;

    public event Action<CameraFrame>? FrameReceived;
    public event Action<string>? ErrorOccurred;

    public bool IsStreaming { get; private set; }
    public int Width => _width;
    public int Height => _height;

    public void Open(uint cameraIndex = 1)
    {
        _handle = NativeMethods.LucamCameraOpen(cameraIndex);
        if (_handle == 0)
            throw new InvalidOperationException(
                $"Не удалось открыть камеру Lumenera (индекс {cameraIndex}). Код ошибки LuCam: {NativeMethods.LucamGetLastError()}.");

        var format = new LucamFrameFormat();
        float frameRate = 0;
        if (!NativeMethods.LucamGetFormat(_handle, ref format, ref frameRate))
            throw new InvalidOperationException(
                $"Не удалось получить формат кадра. Код ошибки LuCam: {NativeMethods.LucamGetLastError()}.");

        _width = (int)format.Width;
        _height = (int)format.Height;
        _pixelFormat = format.PixelFormat;
    }

    public void StartStreaming()
    {
        if (_handle == 0)
            throw new InvalidOperationException("Камера не открыта.");
        if (IsStreaming)
            return;

        _callback = OnFrame;
        _callbackId = NativeMethods.LucamAddStreamingCallback(_handle, _callback, 0);
        if (_callbackId == 0)
            throw new InvalidOperationException(
                $"Не удалось зарегистрировать callback потока. Код ошибки LuCam: {NativeMethods.LucamGetLastError()}.");

        if (!NativeMethods.LucamStreamVideoControl(_handle, NativeMethods.StreamControlStartStreaming, 0))
            throw new InvalidOperationException(
                $"Не удалось запустить видеопоток. Код ошибки LuCam: {NativeMethods.LucamGetLastError()}.");

        IsStreaming = true;
    }

    public void StopStreaming()
    {
        if (!IsStreaming || _handle == 0)
            return;

        NativeMethods.LucamStreamVideoControl(_handle, NativeMethods.StreamControlStopStreaming, 0);

        if (_callbackId != 0)
        {
            NativeMethods.LucamRemoveStreamingCallback(_handle, _callbackId);
            _callbackId = 0;
        }

        _callback = null;
        IsStreaming = false;
    }

    private void OnFrame(nint context, nint pData, int dataLength)
    {
        if (pData == 0 || dataLength <= 0 || _width <= 0 || _height <= 0)
            return;

        try
        {
            var raw = new byte[dataLength];
            Marshal.Copy(pData, raw, 0, dataLength);

            var rgb = new byte[_width * _height * 3];
            var rawHandle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            var rgbHandle = GCHandle.Alloc(rgb, GCHandleType.Pinned);
            try
            {
                bool ok = NativeMethods.LucamConvertFrameToRgb24(
                    _handle,
                    rgbHandle.AddrOfPinnedObject(),
                    rawHandle.AddrOfPinnedObject(),
                    (uint)_width,
                    (uint)_height,
                    _pixelFormat,
                    ImageBitDepth,
                    flipX: false,
                    flipY: false);

                if (!ok)
                {
                    ErrorOccurred?.Invoke($"Ошибка конвертации кадра. Код LuCam: {NativeMethods.LucamGetLastError()}.");
                    return;
                }
            }
            finally
            {
                rawHandle.Free();
                rgbHandle.Free();
            }

            FrameReceived?.Invoke(new CameraFrame { Rgb24 = rgb, Width = _width, Height = _height });
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Исключение при обработке кадра: {ex.Message}");
        }
    }

    public void Close()
    {
        StopStreaming();
        if (_handle != 0)
        {
            NativeMethods.LucamCameraClose(_handle);
            _handle = 0;
        }
    }

    public void Dispose() => Close();
}

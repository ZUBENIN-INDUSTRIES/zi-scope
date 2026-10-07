using System.Runtime.InteropServices;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Camera;

/// <summary>Реальная камера Lumenera через lucamapi.dll.</summary>
public sealed class LuCamCamera : ICamera
{
    private static readonly CameraProperty[] Supported = { CameraProperty.Exposure, CameraProperty.Gain };

    private readonly CameraSettings _settings;
    private readonly object _bufLock = new();
    private nint _handle;
    private int _width, _height;
    private uint _pixelFormat;
    private int _callbackId = -1;
    private NativeMethods.StreamingCallback? _callback;   // держим в поле, чтобы GC не собрал делегат

    // «Последний кадр побеждает»: колбэк пишет в _spare и меняет его местами с _ready
    private byte[]? _spare, _ready, _working;
    private bool _hasReady;
    private long _sequence;
    private Thread? _convertThread;
    private volatile bool _running;
    private AutoResetEvent? _frameSignal;
    private int _rawFrameBytes;                         // минимальная длина сырого кадра

    public LuCamCamera(CameraSettings settings) => _settings = settings;

    public CameraInfo? Info { get; private set; }
    public bool IsStreaming => _running;
    public IReadOnlyList<CameraProperty> SupportedProperties => Supported;
    public event Action<CameraFrame>? FrameReceived;
    public event Action<string>? Error;

    public void Open()
    {
        if (_handle != 0) return;
        _handle = NativeMethods.LucamCameraOpen((uint)Math.Max(1, _settings.CameraIndex));
        if (_handle == 0) throw Fail("Не удалось открыть камеру");

        var fmt = new LucamFrameFormat();
        if (!NativeMethods.LucamGetFormat(_handle, ref fmt, out float rate)) throw Fail("Не удалось прочитать формат кадра");
        if (fmt.PixelFormat == NativeMethods.PF_16)
        {
            fmt.PixelFormat = NativeMethods.PF_8;
            if (!NativeMethods.LucamSetFormat(_handle, ref fmt, rate)) throw Fail("Не удалось переключить камеру в 8-битный режим");
        }
        _pixelFormat = fmt.PixelFormat;
        _width = (int)(fmt.Width / Math.Max((ushort)1, fmt.SubSampleX));
        _height = (int)(fmt.Height / Math.Max((ushort)1, fmt.SubSampleY));
        _rawFrameBytes = _width * _height * BytesPerPixel(_pixelFormat);

        string serial = NativeMethods.LucamQueryVersion(_handle, out var ver) ? ver.SerialNumber.ToString() : "—";
        Info = new CameraInfo("Lumenera INFINITY", serial, _width, _height, false);
    }

    public void Start()
    {
        if (_running) return;
        Open();
        _callback = OnStreamingData;
        _callbackId = NativeMethods.LucamAddStreamingCallback(_handle, _callback, 0);
        if (_callbackId == -1) throw Fail("Не удалось подписаться на поток кадров");

        _frameSignal ??= new AutoResetEvent(false);
        _running = true;
        _convertThread = new Thread(ConvertLoop) { IsBackground = true, Name = "LuCam convert" };
        _convertThread.Start();

        if (!NativeMethods.LucamStreamVideoControl(_handle, NativeMethods.StartStreaming, 0))
        {
            var ex = Fail("Не удалось запустить видеопоток");
            Stop();
            throw ex;
        }
    }

    public void Stop()
    {
        if (_handle != 0) NativeMethods.LucamStreamVideoControl(_handle, NativeMethods.StopStreaming, 0);
        if (_callbackId != -1)
        {
            NativeMethods.LucamRemoveStreamingCallback(_handle, _callbackId);
            _callbackId = -1;
        }
        _running = false;
        _frameSignal?.Set();
        var t = _convertThread;
        _convertThread = null;
        // Из обработчика FrameReceived (поток конвертации) себя не ждём: нативный вызов там уже завершён,
        // а цикл выйдет сам, увидев _running == false.
        if (t is not null && t != Thread.CurrentThread) t.Join();
        _callback = null;
    }

    public void Close()
    {
        Stop();   // поток конвертации завершён (или это он сам) — ConvertFrameToRgb24 больше не выполняется
        if (_handle != 0)
        {
            NativeMethods.LucamCameraClose(_handle);
            _handle = 0;
        }
        _frameSignal?.Dispose();
        _frameSignal = null;
        Info = null;
    }

    public void Dispose() => Close();

    public PropertyRange? GetRange(CameraProperty p)
    {
        if (_handle == 0) return null;
        return NativeMethods.LucamPropertyRange(_handle, PropId(p), out float min, out float max, out float def, out _)
            ? new PropertyRange(min, max, def) : null;
    }

    public float? Get(CameraProperty p)
    {
        if (_handle == 0) return null;
        return NativeMethods.LucamGetProperty(_handle, PropId(p), out float v, out _) ? v : null;
    }

    public bool Set(CameraProperty p, float value)
        => _handle != 0 && NativeMethods.LucamSetProperty(_handle, PropId(p), value, 0);

    private static uint PropId(CameraProperty p) => p switch
    {
        CameraProperty.Exposure => NativeMethods.PropExposure,
        CameraProperty.Gain => NativeMethods.PropGain,
        _ => throw new ArgumentOutOfRangeException(nameof(p)),
    };

    private static int BytesPerPixel(uint pixelFormat) => pixelFormat switch
    {
        0 => 1,   // PF_8
        1 => 2,   // PF_16
        2 => 3,   // PF_24
        3 => 2,   // PF_YUV422
        6 => 4,   // PF_32
        7 => 6,   // PF_48
        _ => 1,
    };

    private static LuCamException Fail(string message) => new(message, SafeLastError());

    private static uint SafeLastError()
    {
        try { return NativeMethods.LucamGetLastError(); } catch { return 0; }
    }

    // Вызывается в потоке драйвера: только копия и сразу выход
    private void OnStreamingData(nint context, nint data, uint length)
    {
        // Неполный кадр не отдаём в ConvertFrameToRgb24: он прочитал бы за концом буфера
        if (data == 0 || length < _rawFrameBytes) return;
        lock (_bufLock)
        {
            if (_spare is null || _spare.Length != length) _spare = new byte[length];
            Marshal.Copy(data, _spare, 0, (int)length);
            (_ready, _spare) = (_spare, _ready);
            _hasReady = true;
        }
        try { _frameSignal?.Set(); } catch (ObjectDisposedException) { }
    }

    private void ConvertLoop()
    {
        var conv = new LucamConversion { DemosaicMethod = (uint)_settings.DemosaicMethod, CorrectionMatrix = 0 };
        var signal = _frameSignal!;
        while (_running)
        {
            signal.WaitOne(500);
            if (!_running) break;
            lock (_bufLock)
            {
                if (!_hasReady) continue;
                (_working, _ready) = (_ready, _working);
                _hasReady = false;
            }
            try
            {
                var bgr = new byte[_width * _height * 3];
                bool ok;
                unsafe
                {
                    fixed (byte* src = _working)
                    fixed (byte* dst = bgr)
                        ok = NativeMethods.LucamConvertFrameToRgb24(_handle, (nint)dst, (nint)src, (uint)_width, (uint)_height, _pixelFormat, ref conv);
                }
                if (!ok)
                {
                    Error?.Invoke($"Ошибка преобразования кадра (код ошибки LuCam: {SafeLastError()})");
                    continue;
                }
                // Вывод LuCam — BGR; при SwapRedBlue меняем R и B местами
                if (_settings.SwapRedBlue)
                    for (int i = 0; i < bgr.Length; i += 3) (bgr[i], bgr[i + 2]) = (bgr[i + 2], bgr[i]);

                FrameReceived?.Invoke(new CameraFrame
                {
                    Bgr24 = bgr, Width = _width, Height = _height,
                    Sequence = Interlocked.Increment(ref _sequence), Timestamp = DateTime.Now,
                });
            }
            catch (Exception ex)
            {
                Error?.Invoke($"Ошибка обработки кадра: {ex.Message}");
            }
        }
    }
}

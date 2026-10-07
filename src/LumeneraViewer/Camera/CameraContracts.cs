namespace LumeneraViewer.Camera;

/// <summary>Аппаратные параметры камеры. Exposure — в миллисекундах.</summary>
public enum CameraProperty { Exposure, Gain }

public sealed record PropertyRange(float Min, float Max, float Default);

public sealed record CameraInfo(string Model, string Serial, int Width, int Height, bool IsSimulated);

/// <summary>Кадр: пиксели всегда BGR24, stride = Width·3.</summary>
public sealed class CameraFrame
{
    public required byte[] Bgr24 { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public long Sequence { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>Источник кадров. FrameReceived и Error вызываются в фоновом потоке.</summary>
public interface ICamera : IDisposable
{
    CameraInfo? Info { get; }
    bool IsStreaming { get; }
    event Action<CameraFrame> FrameReceived;
    event Action<string> Error;
    void Open();
    void Start();
    void Stop();
    void Close();
    IReadOnlyList<CameraProperty> SupportedProperties { get; }
    PropertyRange? GetRange(CameraProperty p);
    float? Get(CameraProperty p);
    bool Set(CameraProperty p, float value);
}

/// <summary>Ошибка LuCam API: код из LucamGetLastError и русский текст.</summary>
public sealed class LuCamException : Exception
{
    public uint Code { get; }
    public LuCamException(string message, uint code) : base($"{message} (код ошибки LuCam: {code})") => Code = code;
}

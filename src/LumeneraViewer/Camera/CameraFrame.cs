namespace LumeneraViewer.Camera;

public sealed class CameraFrame
{
    public required byte[] Rgb24 { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}

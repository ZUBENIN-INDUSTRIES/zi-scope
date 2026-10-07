using System.IO;
using System.Text.Json;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;

namespace LumeneraViewer.Settings;

public sealed class AppSettings
{
    public CameraSettings Camera { get; set; } = new();
    public ImageAdjustments Adjustments { get; set; } = ImageAdjustments.Default;
    public float? ExposureMs { get; set; }            // последнее значение, null — взять из камеры
    public float? Gain { get; set; }
    public List<AdjustmentPreset> Presets { get; set; } = new();
    public List<ObjectiveProfile> Objectives { get; set; } = new() { new("Объектив 1", null) };
    public string CurrentObjective { get; set; } = "Объектив 1";
    public ScaleBarSettings ScaleBar { get; set; } = new();
    public GuideSettings Guides { get; set; } = new();
    public ExportSettings Export { get; set; } = new();
    public TimelapseSettings Timelapse { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
    public Dictionary<string, JsonElement> Extensions { get; set; } = new(); // для будущих узких функций (R04)
}

public sealed class CameraSettings
{
    public int CameraIndex { get; set; } = 1;
    public bool SwapRedBlue { get; set; }
    public int DemosaicMethod { get; set; } = 2;
    public bool PreferDemo { get; set; }
}

public sealed record AdjustmentPreset(string Name, ImageAdjustments Adjustments, float? ExposureMs, float? Gain);

public sealed record ObjectiveProfile(string Name, double? MicronsPerPixel);

public enum ScaleBarCorner { BottomRight, BottomLeft, TopRight, TopLeft }

public enum OverlayColor { White, Black, Yellow }

public enum LengthUnit { Micrometers, Millimeters }

public sealed class ScaleBarSettings
{
    public bool Enabled { get; set; } = true;
    public ScaleBarCorner Corner { get; set; }
    public OverlayColor Color { get; set; }
    public bool Background { get; set; } = true;
    public double FontSize { get; set; } = 16;
    public double Thickness { get; set; } = 4;
    public double? FixedMicrons { get; set; }
    public LengthUnit Unit { get; set; }
    /// <summary>Позиция после перетаскивания, в долях кадра 0..1; null — угол.</summary>
    public double? CustomX { get; set; }
    public double? CustomY { get; set; }
}

public sealed class GuideSettings
{
    public bool Crosshair { get; set; }
    public bool Grid { get; set; }
    public double GridStepMicrons { get; set; } = 100;
    public double GridStepPercent { get; set; } = 10;
}

public sealed class ExportSettings
{
    public string Folder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "LumeneraCaptures");
    public ImageFormatKind Format { get; set; } = ImageFormatKind.Png;
    public int JpegQuality { get; set; } = 95;
    public bool TiffLzw { get; set; }
    public bool BurnOverlays { get; set; } = true;
    public bool AlsoSaveClean { get; set; }
    public bool WriteMetadata { get; set; } = true;
    public string Prefix { get; set; } = "Lumenera";
}

public sealed class TimelapseSettings
{
    public double IntervalSeconds { get; set; } = 10;
    /// <summary>0 — бесконечно.</summary>
    public int Count { get; set; } = 0;
}

public sealed class WindowSettings
{
    public double Width { get; set; } = 1400;
    public double Height { get; set; } = 880;
    public bool Maximized { get; set; }
    public bool PanelCollapsed { get; set; }
}

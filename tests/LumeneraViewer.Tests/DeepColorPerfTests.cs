using System.Diagnostics;
using LumeneraViewer.Imaging;
using Xunit.Abstractions;

namespace LumeneraViewer.Tests;

/// <summary>Замер скорости запускается только при переменной окружения LV_PERF=1, в обычный прогон не входит.</summary>
public sealed class PerfFactAttribute : FactAttribute
{
    public PerfFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("LV_PERF") != "1")
            Skip = "Замер скорости: $env:LV_PERF=1; dotnet test LumeneraViewer.sln -c Release --filter \"Category=Perf\"";
    }
}

public sealed class DeepColorPerfTests(ITestOutputHelper output)
{
    [PerfFact]
    [Trait("Category", "Perf")]
    public void AllSettings_1920x1440_Within100ms()
    {
        const int w = 1920, h = 1440;
        var src = new byte[w * h * 3];
        new Random(1).NextBytes(src);
        var dst = new byte[src.Length];
        var curve = new ToneCurve([new(0, 10), new(64, 80), new(192, 200), new(255, 250)]);
        var deep = new DeepColor
        {
            RedGain = 1.2, GreenGain = 0.9, BlueGain = 1.1,
            Master = new ChannelLevels(10, 245, 1.1), Red = new ChannelLevels(5, 250, 0.9),
            Green = new ChannelLevels(Gamma: 1.2), Blue = new ChannelLevels(White: 240),
            ExposureEv = 0.5, Highlights = -40, Shadows = 30, Whites = 10, Blacks = -10,
            Clarity = 40, Vibrance = 30, Dehaze = 20,
            Hsl = Enumerable.Range(0, 8).Select(i => new HslAdjust(i * 10 - 30, 20, -10)).ToList(),
            SharpenAmount = 120, SharpenRadius = 3,
            CurveMaster = curve, CurveRed = curve, CurveGreen = curve, CurveBlue = curve,
        };
        var a = new ImageAdjustments(Brightness: 5, Contrast: 10, Gamma: 1.1, Saturation: 110, Temperature: 10, Tint: -5) { Deep = deep };

        for (int i = 0; i < 3; i++) ImageProcessor.Apply(src, dst, w, h, a);   // прогрев
        var times = new List<double>();
        for (int i = 0; i < 15; i++)
        {
            var sw = Stopwatch.StartNew();
            ImageProcessor.Apply(src, dst, w, h, a);
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        double median = times[times.Count / 2];
        output.WriteLine($"1920×1440, все настройки: медиана {median:F1} мс, мин {times[0]:F1}, макс {times[^1]:F1}");
        Assert.True(median <= 100, $"медиана {median:F1} мс > 100 мс");
    }
}

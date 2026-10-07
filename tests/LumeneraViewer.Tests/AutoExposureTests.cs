using LumeneraViewer.Camera;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Tests;

public class AutoExposureTests
{
    private static readonly PropertyRange Range = new(0.1f, 1000f, 10f);

    [Theory]
    [InlineData(0.45, true)]
    [InlineData(0.42, true)]
    [InlineData(0.48, true)]
    [InlineData(0.41, false)]
    [InlineData(0.49, false)]
    public void Цель_045_с_допуском_003(double mean, bool expected) =>
        Assert.Equal(expected, AutoExposure.IsOnTarget(mean));

    [Fact]
    public void Пересвеченный_кадр_уменьшает_экспозицию_пропорционально()
        // 0.9 → множитель 0.45/0.9 = 0.5
        => Assert.Equal(5f, AutoExposure.NextExposure(10f, 0.9, Range), 3);

    [Fact]
    public void Множитель_не_больше_4()
        // 0.45/0.05 = 9 → обрезается до 4
        => Assert.Equal(40f, AutoExposure.NextExposure(10f, 0.05, Range), 3);

    [Fact]
    public void Множитель_не_меньше_четверти()
        // 0.45/2.0 = 0.225 → обрезается до 0.25
        => Assert.Equal(2.5f, AutoExposure.NextExposure(10f, 2.0, Range), 3);

    [Fact]
    public void Чёрный_кадр_даёт_максимальный_шаг()
        => Assert.Equal(40f, AutoExposure.NextExposure(10f, 0.0, Range), 3);

    [Fact]
    public void Результат_в_пределах_диапазона_камеры()
    {
        Assert.Equal(1000f, AutoExposure.NextExposure(900f, 0.1, Range), 3);
        Assert.Equal(0.1f, AutoExposure.NextExposure(0.2f, 1.0, Range), 3);
    }
}

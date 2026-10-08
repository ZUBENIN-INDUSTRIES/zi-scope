using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Camera;
using LumeneraViewer.Imaging;

namespace LumeneraViewer.ViewModels;

/// <summary>Карточка «Камера»: экспозиция, усиление, автоэкспозиция, гистограмма, фокус-ассистент.</summary>
public partial class CameraPanelViewModel : ObservableObject
{
    private const int AnalysisIntervalMs = 100; // не чаще 10 раз в секунду
    private const double MarkerAt = 1 / 1.2;     // максимум за сессию рисуется на 83 % полосы

    private readonly Dispatcher _ui;
    private bool _loading;
    private int _histBusy, _sharpBusy;
    private long _histLast, _sharpLast;

    public MainViewModel Host { get; }

    public CameraPanelViewModel(MainViewModel host)
    {
        Host = host;
        _ui = host.Owner.Dispatcher;
        host.CameraChanged += () =>
        {
            CancelAutoWait();   // ожидание кадра автоэкспозиции относится к старой камере
            if (_ui.CheckAccess()) Reload();
            else _ui.BeginInvoke(Reload);
        };
        // Автоэкспозиция относится к увеличению, на котором её запустили: иначе она допишет экспозицию в профиль нового.
        host.MagnificationChanged += _ => InterruptAuto();
        host.SettingsReplaced += InterruptAuto;
        host.ProcessedFrameReady += OnProcessedFrame;
        host.RawFrameReady += OnRawFrame;
        Reload();
    }

    // ───── Экспозиция ─────

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunAutoCommand))] private bool _exposureAvailable;
    [ObservableProperty] private double _exposureMin = 0.1;
    [ObservableProperty] private double _exposureMax = 1000;
    [ObservableProperty] private double _exposureDefault = 10;
    [ObservableProperty] private double _exposureMs = 10;

    /// <summary>Позиция логарифмического слайдера: log10(мс).</summary>
    public double ExposureLog
    {
        get => Math.Log10(Math.Max(ExposureMs, 1e-6));
        set => ExposureMs = RoundSignificant(Math.Pow(10, value));
    }
    public double ExposureLogMin => Math.Log10(Math.Max(ExposureMin, 1e-6));
    public double ExposureLogMax => Math.Log10(Math.Max(ExposureMax, 1e-6));

    partial void OnExposureMinChanged(double value) => OnPropertyChanged(nameof(ExposureLogMin));
    partial void OnExposureMaxChanged(double value) => OnPropertyChanged(nameof(ExposureLogMax));

    partial void OnExposureMsChanged(double value)
    {
        if (ExposureAvailable && (value < ExposureMin || value > ExposureMax))
        {
            ExposureMs = Math.Clamp(value, ExposureMin, ExposureMax);
            return;
        }
        OnPropertyChanged(nameof(ExposureLog));
        if (_loading || !ExposureAvailable || Host.Camera is not { } cam) return;
        cam.Set(CameraProperty.Exposure, (float)value);
        Host.Settings.ExposureMs = (float)value;
        Host.SaveSettingsSoon();
    }

    public void ResetExposure()
    {
        if (ExposureAvailable) ExposureMs = ExposureDefault;
    }

    // ───── Усиление ─────

    [ObservableProperty] private bool _gainAvailable;
    [ObservableProperty] private double _gainMin = 1;
    [ObservableProperty] private double _gainMax = 16;
    [ObservableProperty] private double _gainDefault = 1;
    [ObservableProperty] private double _gain = 1;

    partial void OnGainChanged(double value)
    {
        if (_loading || !GainAvailable || Host.Camera is not { } cam) return;
        cam.Set(CameraProperty.Gain, (float)value);
        Host.Settings.Gain = (float)value;
        Host.SaveSettingsSoon();
    }

    /// <summary>Подсказка, почему регуляторы неактивны; null — всё доступно.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPropertiesHint))] private string? _propertiesHint;
    public bool HasPropertiesHint => PropertiesHint is not null;

    /// <summary>Текущие значения для пресета (null — свойство недоступно).</summary>
    public float? CurrentExposure => ExposureAvailable ? (float)ExposureMs : null;
    public float? CurrentGain => GainAvailable ? (float)Gain : null;

    /// <summary>Применить экспозицию/усиление из пресета (вне диапазона — обрезается).</summary>
    public void ApplyExposureGain(float? exposureMs, float? gain)
    {
        if (exposureMs is { } e && ExposureAvailable) ExposureMs = Math.Clamp(e, ExposureMin, ExposureMax);
        if (gain is { } g && GainAvailable) Gain = Math.Clamp(g, GainMin, GainMax);
    }

    /// <summary>Перечитать диапазоны и значения из новой камеры, применить сохранённые.</summary>
    private void Reload()
    {
        _loading = true;
        try
        {
            var cam = Host.Camera;
            IsSimulated = cam is SimulatedCamera;
            if (cam is SimulatedCamera sim) _demoFocus = sim.Focus;
            OnPropertyChanged(nameof(DemoFocus));

            var er = cam?.GetRange(CameraProperty.Exposure);
            ExposureAvailable = er is not null;
            if (er is not null)
            {
                ExposureMin = er.Min;
                ExposureMax = er.Max;
                ExposureDefault = er.Default;
                var saved = Host.Settings.ExposureMs;
                if (saved is { } s && s >= er.Min && s <= er.Max && cam!.Set(CameraProperty.Exposure, s)) ExposureMs = s;
                else ExposureMs = cam!.Get(CameraProperty.Exposure) ?? er.Default;
            }

            var gr = cam?.GetRange(CameraProperty.Gain);
            GainAvailable = gr is not null;
            if (gr is not null)
            {
                GainMin = gr.Min;
                GainMax = gr.Max;
                GainDefault = gr.Default;
                var saved = Host.Settings.Gain;
                if (saved is { } s && s >= gr.Min && s <= gr.Max && cam!.Set(CameraProperty.Gain, s)) Gain = s;
                else Gain = cam!.Get(CameraProperty.Gain) ?? gr.Default;
            }

            PropertiesHint = cam is null ? "Камера не подключена — регуляторы неактивны."
                : !ExposureAvailable && !GainAvailable ? "Камера не поддерживает экспозицию и усиление."
                : !ExposureAvailable ? "Камера не поддерживает экспозицию."
                : !GainAvailable ? "Камера не поддерживает усиление."
                : null;

            if (cam is null) Histogram = null;
            ResetFocusMax();
            Sharpness = double.NaN;
        }
        finally
        {
            _loading = false;
        }
    }

    // ───── Автоэкспозиция ─────

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunAutoCommand)), NotifyPropertyChangedFor(nameof(AutoText))]
    private bool _isAutoRunning;
    public string AutoText => IsAutoRunning ? "Подбираю экспозицию…" : "Автоэкспозиция";

    private bool CanRunAuto => ExposureAvailable && !IsAutoRunning;

    [RelayCommand(CanExecute = nameof(CanRunAuto))]
    private async Task RunAuto()
    {
        var cam = Host.Camera;
        var range = cam?.GetRange(CameraProperty.Exposure);
        if (cam is null || range is null) return;
        var epoch = Volatile.Read(ref _autoEpoch);
        IsAutoRunning = true;
        try
        {
            var mean = await WaitMeanAsync(0);
            for (int i = 0; ; i++)
            {
                if (Volatile.Read(ref _autoEpoch) != epoch)
                {
                    Host.Notify("Автоэкспозиция прервана", "Сменилось увеличение или конфигурация.", NotifyKind.Warning);
                    return;
                }
                if (mean is not { } m)
                {
                    Host.Notify("Автоэкспозиция", "Камера не присылает кадры.", NotifyKind.Warning);
                    return;
                }
                if (AutoExposure.IsOnTarget(m))
                {
                    Host.Notify("Автоэкспозиция", $"Готово: {FormatMs(ExposureMs)} мс, яркость {m * 100:0} %.", NotifyKind.Success);
                    return;
                }
                if (i >= AutoExposure.MaxIterations || !ReferenceEquals(Host.Camera, cam)) break;
                var next = RoundSignificant(AutoExposure.NextExposure((float)ExposureMs, m, range));
                if (Math.Abs(next - ExposureMs) < 1e-9) break; // упёрлись в предел диапазона
                ExposureMs = next;
                mean = await WaitMeanAsync(AutoExposure.SettleFrames);
            }
            Host.Notify("Автоэкспозиция: не удалось достичь цели",
                $"Экспозиция {FormatMs(ExposureMs)} мс. Попробуйте усиление или освещение.", NotifyKind.Warning);
        }
        finally
        {
            IsAutoRunning = false;
        }
    }

    private CancellationTokenSource? _autoWait;
    private int _autoEpoch;

    private void InterruptAuto()
    {
        Interlocked.Increment(ref _autoEpoch);
        CancelAutoWait();
    }

    private void CancelAutoWait()
    {
        try { Volatile.Read(ref _autoWait)?.Cancel(); }
        catch (ObjectDisposedException) { /* ожидание уже завершилось */ }
    }

    /// <summary>
    /// Пропустить <paramref name="skip"/> кадров и вернуть среднюю яркость центра следующего (null — кадров нет,
    /// таймаут или смена камеры). Яркость считается в пуле потоков, не в потоке камеры; таймер ожидания
    /// освобождается, как только пришёл кадр.
    /// </summary>
    private Task<double?> WaitMeanAsync(int skip)
    {
        var tcs = new TaskCompletionSource<double?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = (int)Math.Min(ExposureMs * (skip + 2) + 3000, 60000);
        var cts = new CancellationTokenSource(timeout);
        CancelAutoWait();
        _autoWait = cts;
        int seen = 0;
        Action<CameraFrame>? handler = null;
        handler = f =>
        {
            if (Interlocked.Increment(ref seen) <= skip) return;
            Host.RawFrameReady -= handler;
            if (cts.IsCancellationRequested) return;
            _ = Task.Run(() =>
            {
                try { tcs.TrySetResult(FrameAnalysis.MeanLuma(f.Bgr24, f.Width, f.Height)); }
                catch { tcs.TrySetResult(null); }
            });
        };
        var reg = cts.Token.Register(() =>
        {
            Host.RawFrameReady -= handler;
            tcs.TrySetResult(null);
        });
        Host.RawFrameReady += handler;
        tcs.Task.ContinueWith(_ =>
        {
            reg.Dispose();
            Host.RawFrameReady -= handler;
            Interlocked.CompareExchange(ref _autoWait, null, cts);
            cts.Dispose();   // таймер ожидания не висит
        }, TaskScheduler.Default);
        return tcs.Task;
    }

    // ───── Гистограмма ─────

    [ObservableProperty] private Histogram? _histogram;
    [ObservableProperty] private double _clippedPercent;

    public bool IsOverexposed => ClippedPercent > 1;
    partial void OnClippedPercentChanged(double value) => OnPropertyChanged(nameof(IsOverexposed));

    private void OnProcessedFrame(CameraFrame f)
    {
        if (!TryEnter(ref _histBusy, ref _histLast)) return;
        var lease = Host.RetainFrame(f);   // буфер кадра не переиспользуется, пока считаем
        Task.Run(() =>
        {
            try
            {
                Histogram h;
                using (lease) h = FrameAnalysis.ComputeHistogram(f.Bgr24, f.Width, f.Height, 4);
                _ui.BeginInvoke(() =>
                {
                    if (Host.Camera is null) return;
                    Histogram = h;
                    ClippedPercent = h.ClippedFraction * 100;
                });
            }
            catch { /* кадр мог оказаться битым — следующий пересчитает */ }
            finally { Volatile.Write(ref _histBusy, 0); }
        });
    }

    // ───── Фокус-ассистент ─────

    [ObservableProperty] private double _sharpness = double.NaN;
    [ObservableProperty] private double _sharpnessMax;
    [ObservableProperty] private double _sharpnessLevel;
    [ObservableProperty] private double _sharpnessPercent = double.NaN;
    public double SharpnessMarker => MarkerAt;

    private void OnRawFrame(CameraFrame f)
    {
        if (!TryEnter(ref _sharpBusy, ref _sharpLast)) return;
        Task.Run(() =>
        {
            try
            {
                var s = FrameAnalysis.ComputeSharpness(f.Bgr24, f.Width, f.Height);
                _ui.BeginInvoke(() => UpdateSharpness(s));
            }
            catch { /* см. гистограмму */ }
            finally { Volatile.Write(ref _sharpBusy, 0); }
        });
    }

    private void UpdateSharpness(double s)
    {
        if (Host.Camera is null || !double.IsFinite(s)) return;
        Sharpness = s;
        if (s > SharpnessMax) SharpnessMax = s;
        SharpnessLevel = SharpnessMax > 0 ? Math.Clamp(s / SharpnessMax * MarkerAt, 0, 1) : 0;
        SharpnessPercent = SharpnessMax > 0 ? s / SharpnessMax * 100 : double.NaN;
    }

    [RelayCommand]
    private void ResetFocusMax()
    {
        SharpnessMax = double.IsFinite(Sharpness) ? Sharpness : 0;
        SharpnessLevel = SharpnessMax > 0 ? MarkerAt : 0;
        SharpnessPercent = SharpnessMax > 0 ? 100 : double.NaN;
    }

    // ───── Фокус (демо) ─────

    [ObservableProperty] private bool _isSimulated;
    private double _demoFocus = 0.5;

    /// <summary>Фокус симулятора 0…1 (0.5 — резко). Только для демо-режима.</summary>
    public double DemoFocus
    {
        get => _demoFocus;
        set
        {
            if (!SetProperty(ref _demoFocus, Math.Clamp(value, 0, 1))) return;
            if (Host.Camera is SimulatedCamera sim) sim.Focus = (float)_demoFocus;
        }
    }

    // ───── Вспомогательное ─────

    private static bool TryEnter(ref int busy, ref long last)
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref last) < AnalysisIntervalMs) return false;
        if (Interlocked.Exchange(ref busy, 1) == 1) return false;
        Interlocked.Exchange(ref last, now);
        return true;
    }

    /// <summary>Три значащие цифры: 12.3, 1.23, 123.</summary>
    internal static double RoundSignificant(double v)
    {
        if (v <= 0 || !double.IsFinite(v)) return v;
        var digits = Math.Clamp(2 - (int)Math.Floor(Math.Log10(v)), 0, 6);
        return Math.Round(v, digits);
    }

    private static string FormatMs(double ms) => ms.ToString(ms < 10 ? "0.##" : "0.#", CultureInfo.CurrentCulture);
}

/// <summary>Шаг программной автоэкспозиции (спецификация, «Автоэкспозиция»).</summary>
public static class AutoExposure
{
    public const double Target = 0.45;
    public const double Tolerance = 0.03;
    public const double MinFactor = 0.25;
    public const double MaxFactor = 4;
    public const int SettleFrames = 2;
    public const int MaxIterations = 8;

    public static bool IsOnTarget(double meanLuma) => Math.Abs(meanLuma - Target) <= Tolerance + 1e-9;

    /// <summary>exp *= clamp(target/mean, 0.25, 4) в пределах диапазона камеры.</summary>
    public static float NextExposure(float currentMs, double meanLuma, PropertyRange range)
    {
        var factor = meanLuma > 0 ? Math.Clamp(Target / meanLuma, MinFactor, MaxFactor) : MaxFactor;
        return (float)Math.Clamp(currentMs * factor, range.Min, range.Max);
    }
}

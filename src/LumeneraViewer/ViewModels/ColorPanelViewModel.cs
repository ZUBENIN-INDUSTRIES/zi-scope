using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Imaging;
using LumeneraViewer.Settings;
using Wpf.Ui.Controls;

using LumeneraViewer.UI;

namespace LumeneraViewer.ViewModels;

/// <summary>Карточка «Цвет»: программная коррекция, авто-ББ, Ч/Б, негатив, пресеты.</summary>
public partial class ColorPanelViewModel : ObservableObject
{
    private bool _syncing;

    public MainViewModel Host { get; }

    public ColorPanelViewModel(MainViewModel host)
    {
        Host = host;
        Presets = new ObservableCollection<AdjustmentPreset>(host.Settings.Presets);
        Presets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoPresets));
        LoadFrom(host.Adjustments);
        // Коррекцию могут поменять снаружи (сброс настроек) — подтягиваем
        host.PropertyChanged += OnHostPropertyChanged;
        host.SettingsReplaced += ReloadPresets;
    }

    /// <summary>Настройки заменены (сброс): пресеты перечитываются, удалённые не вернутся при сохранении.</summary>
    private void ReloadPresets()
    {
        _selectingSilently = true;
        SelectedPreset = null;
        _selectingSilently = false;
        Presets.Clear();
        foreach (var p in Host.Settings.Presets ??= new()) Presets.Add(p);
    }

    // Диапазоны и значения по умолчанию — спецификация, истории 17–19
    [ObservableProperty] private double _brightness;
    [ObservableProperty] private double _contrast;
    [ObservableProperty] private double _gamma = 1;
    [ObservableProperty] private double _saturation = 100;
    [ObservableProperty] private double _temperature;
    [ObservableProperty] private double _tint;
    [ObservableProperty] private bool _grayscale;
    [ObservableProperty] private bool _negative;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_syncing) return;
        if (e.PropertyName is nameof(Brightness) or nameof(Contrast) or nameof(Gamma) or nameof(Saturation)
            or nameof(Temperature) or nameof(Tint) or nameof(Grayscale) or nameof(Negative))
            Host.Adjustments = Build(); // хост сам сохраняет настройки (SaveSettingsSoon)
    }

    private ImageAdjustments Build() => new(
        Math.Round(Brightness), Math.Round(Contrast), Math.Round(Gamma, 2), Math.Round(Saturation),
        Math.Round(Temperature), Math.Round(Tint), Grayscale, Negative);

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Adjustments) && Host.Adjustments != Build()) LoadFrom(Host.Adjustments);
    }

    private void LoadFrom(ImageAdjustments a)
    {
        _syncing = true;
        try
        {
            Brightness = a.Brightness;
            Contrast = a.Contrast;
            Gamma = a.Gamma;
            Saturation = a.Saturation;
            Temperature = a.Temperature;
            Tint = a.Tint;
            Grayscale = a.Grayscale;
            Negative = a.Negative;
        }
        finally
        {
            _syncing = false;
        }
        Host.Adjustments = Build();
    }

    [RelayCommand]
    private void ResetAll()
    {
        LoadFrom(ImageAdjustments.Default);
        SelectedPreset = null;
    }

    // ───── Авто-баланс белого ─────

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AutoWhiteBalanceCommand))] private bool _isAutoWbRunning;

    private bool CanAutoWb => !IsAutoWbRunning;

    [RelayCommand(CanExecute = nameof(CanAutoWb))]
    private async Task AutoWhiteBalance()
    {
        var frame = Host.LatestRawFrame;
        if (frame is null)
        {
            Host.Notify("Авто-ББ", "Нет кадра для анализа.", NotifyKind.Warning);
            return;
        }
        IsAutoWbRunning = true;
        try
        {
            var (t, tint) = await Task.Run(() =>
                ImageProcessor.GainsToTemperatureTint(ImageProcessor.ComputeGrayWorldGains(frame.Bgr24, frame.Width, frame.Height)));
            _syncing = true;
            try
            {
                Temperature = Math.Round(Math.Clamp(t, -100, 100));
                Tint = Math.Round(Math.Clamp(tint, -100, 100));
            }
            finally { _syncing = false; }
            Host.Adjustments = Build();
            Host.Notify("Авто-ББ", $"Температура {Temperature:+0;−0;0}, оттенок {Tint:+0;−0;0}.", NotifyKind.Success);
        }
        catch (Exception ex)
        {
            Host.Notify("Авто-ББ не удался", ex.Message, NotifyKind.Error);
        }
        finally
        {
            IsAutoWbRunning = false;
        }
    }

    // ───── Пресеты ─────

    public ObservableCollection<AdjustmentPreset> Presets { get; }
    public bool HasNoPresets => Presets.Count == 0;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ApplyPresetCommand), nameof(DeletePresetCommand))]
    private AdjustmentPreset? _selectedPreset;

    private bool HasSelection => SelectedPreset is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ApplyPreset()
    {
        if (SelectedPreset is not { } p) return;
        LoadFrom(p.Adjustments ?? ImageAdjustments.Default);
        Host.CameraPanel.ApplyExposureGain(p.ExposureMs, p.Gain);
        Host.Notify("Пресет применён", p.Name, NotifyKind.Success);
    }

    [RelayCommand]
    private async Task SavePreset()
    {
        var name = await Dialogs.AskTextAsync(Host.Owner, "Сохранить пресет",
            "Сохранятся коррекция цвета, экспозиция и усиление.",
            SelectedPreset?.Name ?? $"Пресет {Presets.Count + 1}", "Сохранить",
            "Например: «Окраска по Граму, ×40»");
        if (name is null) return;
        var preset = new AdjustmentPreset(name, Build(), Host.CameraPanel.CurrentExposure, Host.CameraPanel.CurrentGain);
        var existing = Presets.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null) Presets[Presets.IndexOf(existing)] = preset; // перезапись по имени
        else Presets.Add(preset);
        _selectingSilently = true;
        SelectedPreset = preset;
        _selectingSilently = false;
        Persist();
        Host.Notify("Пресет сохранён", name, NotifyKind.Success);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeletePreset()
    {
        if (SelectedPreset is not { } p) return;
        if (!await Dialogs.ConfirmAsync(Host.Owner, "Удалить пресет?",
                $"Пресет «{p.Name}» будет удалён без возможности восстановления.", "Удалить")) return;
        Presets.Remove(p);
        SelectedPreset = null;
        Persist();
    }

    private bool _selectingSilently;

    /// <summary>Выбор в списке сразу применяет пресет.</summary>
    partial void OnSelectedPresetChanged(AdjustmentPreset? value)
    {
        if (value is not null && !_selectingSilently) ApplyPreset();
    }

    private void Persist()
    {
        Host.Settings.Presets = Presets.ToList();
        Host.SaveSettingsSoon();
    }
}

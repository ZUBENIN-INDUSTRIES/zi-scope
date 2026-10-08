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

/// <summary>Вкладка «Цвет»: программная коррекция текущего увеличения, авто-ББ, Ч/Б, негатив, профили цвета.</summary>
public partial class ColorPanelViewModel : ObservableObject
{
    private bool _syncing;

    public MainViewModel Host { get; }

    public ColorPanelViewModel(MainViewModel host)
    {
        Host = host;
        Profiles = new ObservableCollection<ColorProfile>(host.Settings.ColorProfiles ??= new());
        Profiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoProfiles));
        LoadFrom(host.Adjustments);
        // Коррекцию могут поменять снаружи (сброс настроек) — подтягиваем
        host.PropertyChanged += OnHostPropertyChanged;
        host.SettingsReplaced += ReloadProfiles;
    }

    /// <summary>Настройки заменены (сброс, загрузка конфигурации): профили перечитываются.</summary>
    private void ReloadProfiles()
    {
        SelectedProfile = null;
        Profiles.Clear();
        foreach (var p in Host.Settings.ColorProfiles ??= new()) Profiles.Add(p);
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

    // Глубокие настройки (окно «Глубокий цвет») этой панелью не меняются — сохраняются как есть.
    private ImageAdjustments Build() => new(
        Math.Round(Brightness), Math.Round(Contrast), Math.Round(Gamma, 2), Math.Round(Saturation),
        Math.Round(Temperature), Math.Round(Tint), Grayscale, Negative) { Deep = Host.Adjustments.Deep };

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Adjustments) && Host.Adjustments != Build()) LoadFrom(Host.Adjustments);
    }

    private void LoadFrom(ImageAdjustments a)
    {
        if (Host.Adjustments != a) Host.Adjustments = a;   // глубокие настройки тоже из a
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

    // ───── Профили цвета (история 26) ─────

    public ObservableCollection<ColorProfile> Profiles { get; }
    public bool HasNoProfiles => Profiles.Count == 0;

    /// <summary>Сохранять в новый профиль экспозицию и усиление камеры.</summary>
    [ObservableProperty] private bool _includeExposure;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ApplyProfileCommand), nameof(DeleteProfileCommand),
        nameof(RenameProfileCommand), nameof(ExportProfileCommand))]
    private ColorProfile? _selectedProfile;

    private bool HasSelection => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ApplyProfile()
    {
        if (SelectedProfile is not { } p) return;
        Host.ApplyColorProfile(p);   // ползунки подтянутся по PropertyChanged(Adjustments)
        Host.Notify("Профиль применён", $"«{p.Name}» → ×{Host.CurrentMagnification}", NotifyKind.Success);
    }

    [RelayCommand]
    private async Task SaveProfile()
    {
        var name = await Dialogs.AskTextAsync(Host.Owner, "Сохранить профиль цвета",
            IncludeExposure ? "Сохранятся основные и глубокие настройки цвета, экспозиция и усиление."
                            : "Сохранятся основные и глубокие настройки цвета.",
            SelectedProfile?.Name ?? $"Профиль {Profiles.Count + 1}", "Сохранить", "Например: «Окраска по Граму»");
        if (name is null) return;
        var profile = new ColorProfile
        {
            Name = name,
            Adjustments = Host.Adjustments,
            IncludesExposure = IncludeExposure,
            ExposureMs = IncludeExposure ? Host.CameraPanel.CurrentExposure ?? Host.Settings.ExposureMs : null,
            Gain = IncludeExposure ? Host.CameraPanel.CurrentGain ?? Host.Settings.Gain : null,
        };
        var existing = FindByName(name);
        if (existing is not null)
        {
            if (!await Dialogs.ConfirmAsync(Host.Owner, "Заменить профиль?",
                    $"Профиль «{name}» уже есть. Заменить его текущими настройками?", "Заменить")) return;
            Profiles[Profiles.IndexOf(existing)] = profile;
        }
        else Profiles.Add(profile);
        SelectedProfile = profile;
        Persist();
        Host.Notify("Профиль сохранён", name, NotifyKind.Success);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RenameProfile()
    {
        if (SelectedProfile is not { } p) return;
        var name = await Dialogs.AskTextAsync(Host.Owner, "Переименовать профиль", "Новое название", p.Name, "Сохранить");
        if (name is null || name == p.Name) return;
        if (FindByName(name) is { } other && !ReferenceEquals(other, p))
        {
            Host.Notify("Такое имя уже есть", "Выберите другое название профиля.", NotifyKind.Warning);
            return;
        }
        var i = Profiles.IndexOf(p);
        var renamed = new ColorProfile
        {
            Name = name, Adjustments = p.Adjustments, IncludesExposure = p.IncludesExposure, ExposureMs = p.ExposureMs, Gain = p.Gain,
        };
        Profiles[i] = renamed;   // новый объект — список обновит подпись
        SelectedProfile = renamed;
        Persist();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteProfile()
    {
        if (SelectedProfile is not { } p) return;
        if (!await Dialogs.ConfirmAsync(Host.Owner, "Удалить профиль?",
                $"Профиль цвета «{p.Name}» будет удалён без возможности восстановления.", "Удалить")) return;
        Profiles.Remove(p);
        SelectedProfile = null;
        Persist();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ExportProfile()
    {
        if (SelectedProfile is not { } p) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Экспорт профиля цвета",
            Filter = ProfileFileFilters.Color,
            DefaultExt = ProfileFiles.ColorExtension,
            FileName = ProfileFileFilters.SafeName(p.Name),
        };
        if (dlg.ShowDialog(Host.Owner) != true) return;
        try
        {
            ProfileFiles.WriteColor(dlg.FileName, p);
            Host.Notify("Профиль экспортирован", dlg.FileName, NotifyKind.Success);
        }
        catch (Exception ex)
        {
            Host.Notify("Не удалось экспортировать профиль", ProfileFileFilters.Describe(ex), NotifyKind.Error);
        }
    }

    [RelayCommand]
    private async Task ImportProfile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Импорт профиля цвета", Filter = ProfileFileFilters.Color };
        if (dlg.ShowDialog(Host.Owner) != true) return;
        ColorProfile p;
        try
        {
            p = ProfileFiles.ReadColor(dlg.FileName);
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(Host.Owner, "Профиль не импортирован", ProfileFileFilters.Describe(ex), "Настройки не изменились.");
            return;
        }
        if (string.IsNullOrWhiteSpace(p.Name)) p.Name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
        var baseName = p.Name;
        for (var i = 2; FindByName(p.Name) is not null; i++) p.Name = $"{baseName} ({i})";
        Profiles.Add(p);
        SelectedProfile = p;
        Persist();
        Host.Notify("Профиль импортирован", $"«{p.Name}» — нажмите «Применить», чтобы использовать.", NotifyKind.Success);
    }

    private ColorProfile? FindByName(string name)
        => Profiles.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));

    private void Persist()
    {
        Host.Settings.ColorProfiles = Profiles.ToList();
        Host.SaveSettingsSoon();
    }
}

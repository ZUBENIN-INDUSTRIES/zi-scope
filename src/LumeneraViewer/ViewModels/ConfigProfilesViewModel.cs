using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Settings;
using LumeneraViewer.UI;
using LumeneraViewer.Views;
using Wpf.Ui.Controls;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// «⋯ → Профили конфигурации» (истории 27–28): файлы <c>.zscope</c> в %AppData%\ZI-scope\profiles.
/// Хост диалогов показывает один диалог за раз, поэтому действие, которому нужен свой диалог
/// (подтверждение, имя, файл), закрывает окно профилей, выполняется и окно открывается снова.
/// </summary>
public partial class ConfigProfilesViewModel : ObservableObject
{
    private readonly string _folder;
    private Func<Task<bool>>? _pending;   // true — после действия снова открыть окно профилей
    private ContentDialog? _dialog;

    public MainViewModel Host { get; }
    public ObservableCollection<ConfigProfileItem> Items { get; } = new();

    public ConfigProfilesViewModel(MainViewModel host, string? folder = null)
    {
        Host = host;
        _folder = folder ?? ProfileFiles.ConfigProfilesFolder;
        Reload();
    }

    public bool HasNoItems => Items.Count == 0;
    public string FolderText => _folder;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(RenameCommand), nameof(DeleteCommand), nameof(ExportCommand))]
    private ConfigProfileItem? _selected;

    private bool HasSelection => Selected is not null;

    /// <summary>Показать окно профилей (UI-поток). Возвращается, когда пользователь его закрыл.</summary>
    public static async Task ShowAsync(MainViewModel host)
    {
        var vm = new ConfigProfilesViewModel(host);
        while (true)
        {
            vm._pending = null;
            vm._dialog = new ContentDialog
            {
                Title = "Профили конфигурации",
                Content = new ConfigProfilesView { DataContext = vm },
                CloseButtonText = "Закрыть",
            };
            await Dialogs.ShowAsync(host.Owner, vm._dialog, 620);
            vm._dialog = null;
            if (vm._pending is not { } action) return;
            if (!await action()) return;
            vm.Reload();
        }
    }

    private void Run(Func<Task<bool>> action)
    {
        _pending = action;
        _dialog?.Hide();
    }

    private void Reload()
    {
        var keep = Selected?.Name;
        Items.Clear();
        foreach (var f in ProfileFiles.ListConfigProfiles(_folder)) Items.Add(new ConfigProfileItem(f));
        Selected = Items.FirstOrDefault(i => i.Name == keep);
        OnPropertyChanged(nameof(HasNoItems));
    }

    private string PathFor(string name) => Path.Combine(_folder, ProfileFileFilters.SafeName(name) + ProfileFiles.ConfigExtension);

    // ───── Команды (кнопки внутри окна) ─────

    [RelayCommand]
    private void SaveCurrent() => Run(async () =>
    {
        var name = await Dialogs.AskTextAsync(Host.Owner, "Сохранить конфигурацию",
            "Сохранятся все настройки: увеличения с калибровками и цветом, профили цвета, плашка, сетка, экспорт, шаблон серии, окно.",
            Selected?.Name ?? $"Конфигурация {Items.Count + 1}", "Сохранить", "Например: «Гистология, окраска ГЭ»");
        if (name is null) return true;
        var path = PathFor(name);
        if (File.Exists(path) && !await Dialogs.ConfirmAsync(Host.Owner, "Заменить конфигурацию?",
                $"Профиль «{Path.GetFileNameWithoutExtension(path)}» уже есть. Заменить его текущими настройками?", "Заменить"))
            return true;
        try
        {
            MagnificationSettings.StoreLive(Host.Settings);
            ProfileFiles.WriteConfig(path, Host.Settings);
            Selected = new ConfigProfileItem(path);   // после Reload выделен сохранённый
            Host.Notify("Конфигурация сохранена", Path.GetFileNameWithoutExtension(path), NotifyKind.Success);
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(Host.Owner, "Конфигурация не сохранена", ProfileFileFilters.Describe(ex));
        }
        return true;
    });

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Load()
    {
        if (Selected is not { } item) return;
        Run(async () =>
        {
            AppSettings loaded;
            try
            {
                loaded = ProfileFiles.ReadConfig(item.Path);
            }
            catch (Exception ex)
            {
                await Dialogs.InfoAsync(Host.Owner, "Конфигурация не загружена", ProfileFileFilters.Describe(ex), "Настройки не изменились.");
                return true;
            }
            if (!await Dialogs.ConfirmAsync(Host.Owner, $"Загрузить «{item.Name}»?",
                    "Все текущие настройки будут заменены настройками из профиля. Чтобы их не потерять, сначала сохраните текущую конфигурацию.",
                    "Загрузить")) return true;
            Host.LoadConfiguration(loaded);
            Host.Notify("Конфигурация загружена", item.Name, NotifyKind.Success);
            return false;
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Rename()
    {
        if (Selected is not { } item) return;
        Run(async () =>
        {
            var name = await Dialogs.AskTextAsync(Host.Owner, "Переименовать конфигурацию", "Новое название", item.Name, "Сохранить");
            if (name is null || name == item.Name) return true;
            var path = PathFor(name);
            try
            {
                if (File.Exists(path) && !string.Equals(path, item.Path, StringComparison.OrdinalIgnoreCase))
                {
                    Host.Notify("Такое имя уже есть", "Выберите другое название.", NotifyKind.Warning);
                    return true;
                }
                File.Move(item.Path, path, overwrite: true);
                Selected = new ConfigProfileItem(path);
            }
            catch (Exception ex)
            {
                await Dialogs.InfoAsync(Host.Owner, "Не удалось переименовать", ProfileFileFilters.Describe(ex));
            }
            return true;
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (Selected is not { } item) return;
        Run(async () =>
        {
            if (!await Dialogs.ConfirmAsync(Host.Owner, "Удалить конфигурацию?",
                    $"Профиль «{item.Name}» будет удалён без возможности восстановления. Текущие настройки не изменятся.", "Удалить"))
                return true;
            try { File.Delete(item.Path); }
            catch (Exception ex) { await Dialogs.InfoAsync(Host.Owner, "Не удалось удалить", ProfileFileFilters.Describe(ex)); }
            return true;
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Export()
    {
        if (Selected is not { } item) return;
        Run(async () =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Экспорт конфигурации",
                Filter = ProfileFileFilters.Config,
                DefaultExt = ProfileFiles.ConfigExtension,
                FileName = item.Name,
            };
            if (dlg.ShowDialog(Host.Owner) != true) return true;
            try
            {
                File.Copy(item.Path, dlg.FileName, overwrite: true);
                Host.Notify("Конфигурация экспортирована", dlg.FileName, NotifyKind.Success);
            }
            catch (Exception ex)
            {
                await Dialogs.InfoAsync(Host.Owner, "Не удалось экспортировать", ProfileFileFilters.Describe(ex));
            }
            return true;
        });
    }

    [RelayCommand]
    private void Import() => Run(async () =>
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Импорт конфигурации", Filter = ProfileFileFilters.Config };
        if (dlg.ShowDialog(Host.Owner) != true) return true;
        AppSettings imported;
        try
        {
            imported = ProfileFiles.ReadConfig(dlg.FileName);   // проверка формата до копирования
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(Host.Owner, "Конфигурация не импортирована", ProfileFileFilters.Describe(ex), "Ничего не изменилось.");
            return true;
        }
        var baseName = Path.GetFileNameWithoutExtension(dlg.FileName);
        var name = baseName;
        for (var i = 2; File.Exists(PathFor(name)); i++) name = $"{baseName} ({i})";
        try
        {
            ProfileFiles.WriteConfig(PathFor(name), imported);
            Selected = new ConfigProfileItem(PathFor(name));
            Host.Notify("Конфигурация импортирована", $"«{name}» — выберите её и нажмите «Загрузить».", NotifyKind.Success);
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(Host.Owner, "Конфигурация не импортирована", ProfileFileFilters.Describe(ex));
        }
        return true;
    });
}

/// <summary>Файл профиля конфигурации в списке.</summary>
public sealed class ConfigProfileItem(string path)
{
    public string Path { get; } = path;
    public string Name { get; } = System.IO.Path.GetFileNameWithoutExtension(path);
    public string ModifiedText { get; } = File.Exists(path) ? File.GetLastWriteTime(path).ToString("dd.MM.yyyy HH:mm") : "";
}

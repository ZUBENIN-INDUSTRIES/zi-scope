using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Export;
using LumeneraViewer.UI;

namespace LumeneraViewer.ViewModels;

/// <summary>Миниатюра в ленте. Thumbnail появляется, когда декодирована в фоне.</summary>
public partial class GalleryItem : ObservableObject
{
    public GalleryItem(string path) => Path = path;

    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    [ObservableProperty] private BitmapSource? _thumbnail;
}

/// <summary>
/// Лента снимков: последние 30 файлов папки экспорта (без копий «_clean»), новые — слева.
/// Миниатюры декодируются на отдельном STA-потоке с DecodePixelWidth = 160.
/// </summary>
public partial class GalleryViewModel : ObservableObject
{
    public const int MaxItems = 30;
    private const int ThumbWidth = 160;

    private readonly StaWorker _decoder = new("Миниатюры ленты");
    private int _reloadVersion;

    public MainViewModel Host { get; }
    public ObservableCollection<GalleryItem> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;

    public GalleryViewModel(MainViewModel host)
    {
        Host = host;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        Reload();
    }

    private static bool IsShown(string path) =>
        FrameExporter.SupportedExtensions.Contains(System.IO.Path.GetExtension(path))
        && !System.IO.Path.GetFileNameWithoutExtension(path).EndsWith("_clean", StringComparison.OrdinalIgnoreCase);

    /// <summary>Перечитать папку экспорта (при старте и смене папки). Вызывать из UI-потока.</summary>
    public async void Reload()
    {
        var version = ++_reloadVersion;
        var folder = Host.Settings.Export?.Folder;
        var files = await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return new List<string>();
                return new DirectoryInfo(folder).EnumerateFiles()
                    .Where(f => IsShown(f.FullName))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(MaxItems)
                    .Select(f => f.FullName)
                    .ToList();
            }
            catch (Exception) { return new List<string>(); }   // папка недоступна — лента просто пуста
        });
        if (version != _reloadVersion) return;
        Items.Clear();
        foreach (var f in files)
        {
            var item = new GalleryItem(f);
            Items.Add(item);
            LoadThumbnail(item);
        }
    }

    /// <summary>Новый снимок — в начало ленты. Вызывать из UI-потока.</summary>
    public void Add(string path)
    {
        if (!IsShown(path)) return;
        var folder = Host.Settings.Export?.Folder;
        if (folder is not null && !string.Equals(System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(path)!).TrimEnd('\\'),
                System.IO.Path.GetFullPath(folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return;   // папку успели сменить — снимок не из текущей папки
        if (Items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)) is { } old) Items.Remove(old);
        var item = new GalleryItem(path);
        Items.Insert(0, item);
        while (Items.Count > MaxItems) Items.RemoveAt(Items.Count - 1);
        LoadThumbnail(item);
    }

    private async void LoadThumbnail(GalleryItem item)
    {
        try
        {
            item.Thumbnail = await _decoder.Run(() => Decode(item.Path));
        }
        catch (Exception)
        {
            // Файл удалён или не декодируется — остаётся заглушка с именем.
        }
    }

    private static BitmapSource Decode(string path)
    {
        var bmp = new BitmapImage();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bmp.DecodePixelWidth = ThumbWidth;
        bmp.StreamSource = fs;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    [RelayCommand]
    private void Open(GalleryItem? item)
    {
        if (item is null) return;
        try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) { Host.Notify("Не удалось открыть файл", ex.Message, NotifyKind.Error); }
    }

    [RelayCommand]
    private void ShowInFolder(GalleryItem? item)
    {
        if (item is null) return;
        try
        {
            if (File.Exists(item.Path)) Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
            else Process.Start(new ProcessStartInfo(System.IO.Path.GetDirectoryName(item.Path)!) { UseShellExecute = true });
        }
        catch (Exception ex) { Host.Notify("Не удалось открыть папку", ex.Message, NotifyKind.Error); }
    }

    [RelayCommand]
    private async Task Delete(GalleryItem? item)
    {
        if (item is null) return;
        var ok = await Dialogs.ConfirmAsync(Host.Owner, "Удалить снимок?",
            $"Файл «{item.FileName}», его чистая копия (_clean) и метаданные (.json) будут удалены без корзины.",
            "Удалить");
        if (!ok) return;
        try
        {
            DeleteShot(item.Path);
            Items.Remove(item);
        }
        catch (Exception ex)
        {
            Host.Notify("Не удалось удалить снимок", ex.Message, NotifyKind.Error);
        }
    }

    /// <summary>Удалить снимок и его спутников: «имя_clean.ext» и «имя.json». Отсутствующие файлы пропускаются.</summary>
    internal static void DeleteShot(string path)
    {
        var dir = System.IO.Path.GetDirectoryName(path)!;
        var stem = System.IO.Path.GetFileNameWithoutExtension(path);
        var ext = System.IO.Path.GetExtension(path);
        foreach (var f in new[] { path, System.IO.Path.Combine(dir, stem + "_clean" + ext), System.IO.Path.Combine(dir, stem + ".json") })
            if (File.Exists(f)) File.Delete(f);
    }

    /// <summary>Закрытие окна: остановить поток декодирования миниатюр.</summary>
    public void Close() => _decoder.Dispose();
}

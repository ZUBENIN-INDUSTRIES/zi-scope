using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Settings;
using LumeneraViewer.UI;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Переключатель увеличений в шапке (история 7): сегменты «×100 ×200 …», «+» — новое увеличение,
/// правый клик — удалить профиль, подсказка «не откалиброван».
/// </summary>
public partial class MagnificationBarViewModel : ObservableObject
{
    public static readonly int[] QuickMagnifications = { 100, 200, 500, 1000, 2000 };

    public MainViewModel Host { get; }
    public ObservableCollection<MagnificationItem> Items { get; } = new();

    public MagnificationBarViewModel(MainViewModel host)
    {
        Host = host;
        if (host.AvailableMagnifications is INotifyCollectionChanged cc) cc.CollectionChanged += (_, _) => Rebuild();
        host.PropertyChanged += OnHostChanged;
        Rebuild();
    }

    /// <summary>У текущего увеличения нет калибровки мкм/px.</summary>
    public bool ShowNotCalibrated => !Host.IsCurrentCalibrated;

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentMagnification))
            foreach (var i in Items) i.Refresh();
        if (e.PropertyName is nameof(MainViewModel.CurrentMagnification) or nameof(MainViewModel.IsCurrentCalibrated))
            OnPropertyChanged(nameof(ShowNotCalibrated));
    }

    private void Rebuild()
    {
        Items.Clear();
        foreach (var m in Host.AvailableMagnifications) Items.Add(new MagnificationItem(this, m));
        OnPropertyChanged(nameof(ShowNotCalibrated));
    }

    internal void Select(int mag) => Host.CurrentMagnification = mag;

    /// <summary>«+»: ввод нового увеличения; уже существующее просто выбирается.</summary>
    [RelayCommand]
    private async Task Add()
    {
        var box = new TextBox { MinWidth = 200, PlaceholderText = "Например, 400" };
        var quick = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var q in QuickMagnifications)
        {
            var b = new Button { Content = "×" + q, Margin = new Thickness(0, 0, 6, 6), Appearance = ControlAppearance.Secondary };
            b.Click += (_, _) => { box.Text = q.ToString(CultureInfo.InvariantCulture); box.Focus(); box.SelectAll(); };
            quick.Children.Add(b);
        }
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"Увеличение, × (целое {MagnificationSettings.MinMagnification}–{MagnificationSettings.MaxMagnification})",
            Opacity = 0.75, Margin = new Thickness(0, 0, 0, 10),
        });
        panel.Children.Add(box);
        panel.Children.Add(quick);
        var dialog = new ContentDialog
        {
            Title = "Новое увеличение",
            Content = panel,
            PrimaryButtonText = "Добавить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Loaded += (_, _) => box.Focus();
        if (await Dialogs.ShowAsync(Host.Owner, dialog, 420) != ContentDialogResult.Primary) return;

        var text = box.Text.Trim().TrimStart('×', 'x', 'X');
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mag) || !MagnificationSettings.IsValid(mag))
        {
            Host.Notify("Неверное увеличение",
                $"Введите целое число от {MagnificationSettings.MinMagnification} до {MagnificationSettings.MaxMagnification}.", NotifyKind.Warning);
            return;
        }
        var existed = Host.AvailableMagnifications.Contains(mag);
        Host.AddMagnification(mag);
        Host.CurrentMagnification = mag;
        if (!existed)
            Host.Notify($"Увеличение ×{mag} добавлено", "Откалибруйте его во вкладке «Масштаб».", NotifyKind.Info);
    }

    internal async Task RemoveAsync(int mag)
    {
        if (mag == Host.CurrentMagnification || Host.AvailableMagnifications.Count <= 1)
        {
            Host.Notify("Нельзя удалить", "Текущее и последнее увеличение удалить нельзя.", NotifyKind.Warning);
            return;
        }
        if (!await Dialogs.ConfirmAsync(Host.Owner, $"Удалить профиль ×{mag}?",
                $"Калибровка, длина плашки и цвет увеличения ×{mag} будут удалены.", "Удалить")) return;
        if (!Host.RemoveMagnification(mag))
            Host.Notify("Нельзя удалить", "Текущее и последнее увеличение удалить нельзя.", NotifyKind.Warning);
    }
}

/// <summary>Один сегмент переключателя.</summary>
public partial class MagnificationItem : ObservableObject
{
    private readonly MagnificationBarViewModel _bar;

    public MagnificationItem(MagnificationBarViewModel bar, int magnification)
    {
        _bar = bar;
        Magnification = magnification;
    }

    public int Magnification { get; }
    public string Label => "×" + Magnification.ToString(CultureInfo.InvariantCulture);
    public string RemoveHeader => $"Удалить профиль {Label}";

    public bool IsSelected
    {
        get => _bar.Host.CurrentMagnification == Magnification;
        set
        {
            if (value) _bar.Select(Magnification);
            OnPropertyChanged();
        }
    }

    public bool CanRemove => _bar.Host.CurrentMagnification != Magnification && _bar.Host.AvailableMagnifications.Count > 1;

    internal void Refresh()
    {
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(CanRemove));
    }

    [RelayCommand]
    private Task Remove() => _bar.RemoveAsync(Magnification);
}

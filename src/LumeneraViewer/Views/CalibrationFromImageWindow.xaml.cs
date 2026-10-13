using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LumeneraViewer.UI;
using LumeneraViewer.UI.Effects;
using LumeneraViewer.ViewModels;
using Wpf.Ui.Controls;

namespace LumeneraViewer.Views;

/// <summary>
/// Окно «Калибровка по снимку»: картинка с зумом и сдвигом (ZoomPanHost), поверх — линия с маркерами на концах
/// (перетаскиваются левой кнопкой) и рамка области подписи. Координаты холста = пиксели картинки.
/// </summary>
public partial class CalibrationFromImageWindow : FluentWindow
{
    private const double GrabRadius = 9;    // экранные пиксели
    private const double MarkerSize = 9;    // диаметр кольца маркера, экранные пиксели

    private readonly CalibrationFromImageViewModel _vm;
    private int _dragging;   // 0 — нет, 1 — A, 2 — B

    public CalibrationFromImageWindow(CalibrationFromImageViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        Owner = vm.Host.Owner;
        Picture.Width = Marks.Width = vm.ImageWidth;
        Picture.Height = Marks.Height = vm.ImageHeight;
        vm.PropertyChanged += OnVmChanged;
        vm.CloseRequested += saved => DialogResult = saved;
        Fit.SizeChanged += (_, _) => Redraw();
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UI.Controls.ZoomPanHost.ZoomProperty, typeof(UI.Controls.ZoomPanHost))
            .AddValueChanged(Fit, (_, _) => { ZoomText.Text = $"{Fit.Zoom * 100:0} %"; Redraw(); });
        Loaded += async (_, _) => { Redraw(); await vm.AnalyzeAsync(); ValueBox.Focus(); };
    }

    /// <summary>Выбрать снимок и открыть окно (модально).</summary>
    public static async void PickAndShow(MainViewModel host)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Снимок с масштабной линией", Filter = ProfileFileFilters.Images };
        if (dlg.ShowDialog(host.Owner) != true) return;
        await ShowForAsync(host, dlg.FileName);
    }

    /// <summary>Открыть окно для файла; true — калибровка сохранена. Ошибку чтения показывает диалогом главного окна.</summary>
    public static async Task<bool> ShowForAsync(MainViewModel host, string path)
    {
        CalibrationFromImageViewModel vm;
        try { vm = new CalibrationFromImageViewModel(host, path); }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(host.Owner, "Не удалось открыть снимок", ex.Message);
            return false;
        }
        return new CalibrationFromImageWindow(vm).ShowDialog() == true;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalibrationFromImageViewModel.A) or nameof(CalibrationFromImageViewModel.B)
            or nameof(CalibrationFromImageViewModel.TextRegion))
            Redraw();
    }

    /// <summary>Пикселей картинки на экранный пиксель (толщины и маркеры не зависят от зума).</summary>
    private double Unit => Fit.Zoom > 0 ? 1 / Fit.Zoom : 1;

    private void Fit_Click(object sender, RoutedEventArgs e) => Fit.FitToView();
    private void Actual_Click(object sender, RoutedEventArgs e) => Fit.ActualPixels();

    private void Redraw()
    {
        Marks.Children.Clear();
        var u = Unit;
        if (_vm.TextRegion is { } r)
            Add(new Rectangle
            {
                Width = r.Width, Height = r.Height, Stroke = Palette.WarningBrush, StrokeThickness = 1.5 * u,
                StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false,
            }, r.X, r.Y);
        if (_vm.A is { } a && _vm.B is { } b)
        {
            Marks.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = Brushes.Black, StrokeThickness = 3.5 * u, Opacity = 0.5, IsHitTestVisible = false });
            Marks.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = new SolidColorBrush(Palette.Cyan), StrokeThickness = 1.5 * u, IsHitTestVisible = false });
        }
        if (_vm.A is { } pa) Marker(pa, u);
        if (_vm.B is { } pb) Marker(pb, u);
    }

    /// <summary>Маркер конца: тонкое кольцо с прозрачной серединой (сама точка видна) и точка в центре.</summary>
    private void Marker(Point p, double u)
    {
        var d = MarkerSize * u;
        Add(new Ellipse { Width = d + 2 * u, Height = d + 2 * u, Stroke = Brushes.Black, StrokeThickness = 1 * u, Opacity = 0.6, IsHitTestVisible = false },
            p.X - d / 2 - u, p.Y - d / 2 - u);
        Add(new Ellipse { Width = d, Height = d, Stroke = Palette.AccentBrush, StrokeThickness = 1.5 * u, IsHitTestVisible = false },
            p.X - d / 2, p.Y - d / 2);
        var dot = 2 * u;
        Add(new Ellipse { Width = dot, Height = dot, Fill = Palette.AccentBrush, IsHitTestVisible = false }, p.X - dot / 2, p.Y - dot / 2);
    }

    private void Add(UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        Marks.Children.Add(e);
    }

    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, _vm.ImageWidth), Math.Clamp(p.Y, 0, _vm.ImageHeight));

    private void OnMarksDown(object sender, MouseButtonEventArgs e)
    {
        var p = Clamp(e.GetPosition(Marks));
        var grab = GrabRadius * Unit;
        if (_vm.A is { } a && (p - a).Length <= grab) _dragging = 1;
        else if (_vm.B is { } b && (p - b).Length <= grab) _dragging = 2;
        else
        {
            if (_vm.NeedsManualLine) _vm.Click(p);
            return;
        }
        Marks.CaptureMouse();
        e.Handled = true;
    }

    private void OnMarksMove(object sender, MouseEventArgs e)
    {
        if (_dragging == 0) return;
        var p = Clamp(e.GetPosition(Marks));
        if (_dragging == 1) _vm.A = p; else _vm.B = p;
    }

    private void OnMarksUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = 0;
        Marks.ReleaseMouseCapture();
    }
}

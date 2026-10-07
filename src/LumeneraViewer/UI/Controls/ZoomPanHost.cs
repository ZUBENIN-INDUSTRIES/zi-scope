using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LumeneraViewer.UI.Controls;

/// <summary>
/// Зум и панорама для превью. Child имеет размер кадра в пикселях (Width/Height заданы явно)
/// и получает трансформ масштаба/сдвига; всё, что лежит внутри Child, видит координаты кадра.
/// Колесо — зум к курсору (от «вписать» до 800 %), левый drag — панорама (если LeftButtonPans),
/// средняя/правая кнопка — панорама всегда, двойной клик — «вписать».
/// </summary>
public sealed class ZoomPanHost : Decorator
{
    public const double MaxScale = 8.0;

    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _shift = new();
    private bool _fit = true;
    private Size _lastContent;
    private Point _panStart;
    private Point _panOrigin;
    private MouseButton? _panButton;

    /// <summary>Текущий масштаб (1 = 100 %). Только для чтения снаружи — пишет сам контрол.</summary>
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(ZoomPanHost),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty LeftButtonPansProperty = DependencyProperty.Register(
        nameof(LeftButtonPans), typeof(bool), typeof(ZoomPanHost), new PropertyMetadata(true));

    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public bool LeftButtonPans { get => (bool)GetValue(LeftButtonPansProperty); set => SetValue(LeftButtonPansProperty, value); }

    public bool IsFit => _fit;

    public ZoomPanHost()
    {
        ClipToBounds = true;
        Focusable = false;
    }

    protected override void OnVisualChildrenChanged(DependencyObject added, DependencyObject removed)
    {
        base.OnVisualChildrenChanged(added, removed);
        if (added is FrameworkElement fe)
        {
            fe.HorizontalAlignment = HorizontalAlignment.Left;
            fe.VerticalAlignment = VerticalAlignment.Top;
            fe.RenderTransform = new TransformGroup { Children = { _scale, _shift } };
        }
    }

    private Size Content => Child?.DesiredSize ?? default;

    private double FitScale
    {
        get
        {
            var c = Content;
            if (c.Width < 1 || c.Height < 1 || ActualWidth < 1 || ActualHeight < 1) return 1;
            return Math.Min(ActualWidth / c.Width, ActualHeight / c.Height);
        }
    }

    private double MinScale => Math.Min(FitScale, MaxScale);

    protected override Size MeasureOverride(Size constraint)
    {
        Child?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(double.IsInfinity(constraint.Width) ? 0 : constraint.Width,
                        double.IsInfinity(constraint.Height) ? 0 : constraint.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is null) return arrangeSize;
        var c = Content;
        Child.Arrange(new Rect(c));
        if (c != _lastContent) { _lastContent = c; _fit = true; }
        // Размер окна изменится после Arrange — применяем трансформ, когда ActualWidth уже новый.
        Dispatcher.BeginInvoke(() => { if (_fit) ApplyFit(); else ApplyScale(Math.Max(_scale.ScaleX, MinScale), null); });
        return arrangeSize;
    }

    /// <summary>«Вписать» (двойной клик, Ctrl+0).</summary>
    public void FitToView()
    {
        _fit = true;
        ApplyFit();
    }

    /// <summary>Масштаб 1:1 относительно центра видимой области (Ctrl+1).</summary>
    public void ActualPixels() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1.0);

    private void ApplyFit()
    {
        var s = MinScale;
        _scale.ScaleX = _scale.ScaleY = s;
        Clamp();
        Publish();
    }

    private void ApplyScale(double s, Point? anchor)
    {
        var old = _scale.ScaleX;
        s = Math.Clamp(s, MinScale, MaxScale);
        if (anchor is Point p && old > 0)
        {
            _shift.X = p.X - (p.X - _shift.X) * s / old;
            _shift.Y = p.Y - (p.Y - _shift.Y) * s / old;
        }
        _scale.ScaleX = _scale.ScaleY = s;
        _fit = s <= FitScale + 1e-6;
        Clamp();
        Publish();
    }

    public void ZoomAt(Point viewPoint, double newScale) => ApplyScale(newScale, viewPoint);

    private void Publish()
    {
        Zoom = _scale.ScaleX;
        if (Child is not null)
            RenderOptions.SetBitmapScalingMode(Child, _scale.ScaleX >= 2 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.Linear);
    }

    // Картинка меньше области — по центру; больше — без пустых полей.
    private void Clamp()
    {
        var c = Content;
        var s = _scale.ScaleX;
        _shift.X = ClampAxis(_shift.X, c.Width * s, ActualWidth);
        _shift.Y = ClampAxis(_shift.Y, c.Height * s, ActualHeight);
    }

    private static double ClampAxis(double t, double scaled, double view) =>
        scaled <= view ? (view - scaled) / 2 : Math.Clamp(t, view - scaled, 0);

    protected override void OnRender(DrawingContext dc) =>
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize)); // чтобы пустые поля ловили мышь

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var factor = Math.Pow(1.2, e.Delta / 120.0);
        ZoomAt(e.GetPosition(this), _scale.ScaleX * factor);
        e.Handled = true;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        // Средняя и правая кнопки панорамируют всегда, даже поверх инструментов оверлея.
        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right) { BeginPan(e); e.Handled = true; }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.Handled || !LeftButtonPans) return;
        if (e.ClickCount == 2) { FitToView(); e.Handled = true; return; }
        BeginPan(e);
        e.Handled = true;
    }

    private void BeginPan(MouseButtonEventArgs e)
    {
        _panButton = e.ChangedButton;
        _panStart = e.GetPosition(this);
        _panOrigin = new Point(_shift.X, _shift.Y);
        CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_panButton is null) return;
        var p = e.GetPosition(this);
        _shift.X = _panOrigin.X + p.X - _panStart.X;
        _shift.Y = _panOrigin.Y + p.Y - _panStart.Y;
        Clamp();
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        if (_panButton != e.ChangedButton) return;
        _panButton = null;
        ReleaseMouseCapture();
        Cursor = null;
        if (e.ChangedButton != MouseButton.Left) e.Handled = true; // не открывать контекстное меню после панорамы
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _panButton = null;
        Cursor = null;
    }
}

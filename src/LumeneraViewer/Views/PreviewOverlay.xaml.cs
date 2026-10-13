using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LumeneraViewer.Overlays;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Views;

/// <summary>
/// Оверлей превью: плашка масштаба, измерения, перекрестие, сетка — через OverlayRenderer.
/// Линейка и калибровка: клик-клик (Shift — строго по горизонтали/вертикали). Плашку можно перетащить.
/// </summary>
public partial class PreviewOverlay : UserControl
{
    private readonly Surface _surface;
    private MainViewModel? _vm;
    private Point? _start;          // первая точка линейки/калибровки
    private Point _end;
    private Vector? _dragOffset;    // перетаскивание плашки: курсор − левый верхний угол

    public PreviewOverlay()
    {
        InitializeComponent();
        _surface = new Surface(this);
        Content = _surface;
        DataContextChanged += (_, _) => Attach(DataContext as MainViewModel);
        Unloaded += (_, _) => { Attach(null); HookAncestor(null); };
        Loaded += (_, _) => { Attach(DataContext as MainViewModel); HookAncestor(FindPanHost()); };
    }

    // ───── Клик вне кадра между первым и вторым кликом линейки/калибровки ─────
    // Оверлей размером с кадр: клик мимо кадра до него не доходит и ушёл бы в панораму ZoomPanHost.
    // Поэтому ловим его туннельным событием предка: точка ограничивается кадром, построение завершается.

    private UIElement? _panHost;

    private UIElement? FindPanHost()
    {
        UIElement? last = null;
        for (DependencyObject? d = VisualTreeHelper.GetParent(this); d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is UI.Controls.ZoomPanHost z) return z;
            if (d is UIElement u) last = u;
        }
        return last;
    }

    private void HookAncestor(UIElement? host)
    {
        if (_panHost is not null)
        {
            _panHost.PreviewMouseLeftButtonDown -= OnHostPreviewLeftDown;
            _panHost.PreviewMouseMove -= OnHostPreviewMove;
        }
        _panHost = host;
        if (host is not null)
        {
            host.PreviewMouseLeftButtonDown += OnHostPreviewLeftDown;
            host.PreviewMouseMove += OnHostPreviewMove;
        }
    }

    private bool IsOutsideFrame(Point p) => p.X < 0 || p.Y < 0 || p.X > FrameSize.Width || p.Y > FrameSize.Height;

    private void OnHostPreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (_start is null || _vm?.ActiveTool is not (PreviewTool.Ruler or PreviewTool.Calibrate)) return;
        var p = e.GetPosition(this);
        if (!IsOutsideFrame(p)) return;   // клик по кадру обработает сам оверлей
        e.Handled = true;                 // не панорамировать
        ToolClick(Clamp(p));
    }

    private void OnHostPreviewMove(object sender, MouseEventArgs e)
    {
        if (_start is null) return;
        var p = e.GetPosition(this);
        if (!IsOutsideFrame(p)) return;
        _end = Snap(Clamp(p));
        Redraw();
    }

    private void Attach(MainViewModel? vm)
    {
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnHostChanged;
            _vm.ScalePanel.Changed -= Redraw;
        }
        _vm = vm;
        if (vm is not null)
        {
            vm.PropertyChanged += OnHostChanged;
            vm.ScalePanel.Changed += Redraw;
        }
        UpdateCursor();
        Redraw();
    }

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.PreviewZoom):
            case nameof(MainViewModel.FrameWidth):
            case nameof(MainViewModel.FrameHeight):
                Redraw();
                break;
            case nameof(MainViewModel.ActiveTool):
                _start = null;
                UpdateCursor();
                Redraw();
                break;
        }
    }

    private void Redraw() => _surface.InvalidateVisual();

    /// <summary>Экранные направляющие: постоянный размер на экране при любом зуме.</summary>
    private double Scale => _vm is { PreviewZoom: > 0 and var z } ? 1 / z : 1;
    /// <summary>Плашка и измерения: как в файле.</summary>
    private double BarScale => OverlayRenderer.PreviewScale(FrameSize);
    private Size FrameSize => _vm is null ? Size.Empty : new Size(_vm.FrameWidth, _vm.FrameHeight);

    private OverlayState? CurrentState()
    {
        if (_vm is null) return null;
        var s = _vm.ScalePanel.BuildOverlayState();
        return _start is { } a ? s with { PendingLine = new Measurement(a, _end) } : s;
    }

    private void UpdateCursor()
        => Cursor = _vm?.ActiveTool is PreviewTool.Ruler or PreviewTool.Calibrate ? Cursors.Cross : null;

    private Point Snap(Point p)
    {
        if (_start is not { } a || (Keyboard.Modifiers & ModifierKeys.Shift) == 0) return p;
        return Math.Abs(p.X - a.X) >= Math.Abs(p.Y - a.Y) ? new Point(p.X, a.Y) : new Point(a.X, p.Y);
    }

    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, FrameSize.Width), Math.Clamp(p.Y, 0, FrameSize.Height));

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_vm is null || FrameSize.IsEmpty || FrameSize.Width <= 0) return;
        var p = Clamp(e.GetPosition(this));

        if (_vm.ActiveTool is PreviewTool.Ruler or PreviewTool.Calibrate)
        {
            e.Handled = true;
            ToolClick(p);
            return;
        }

        if (_vm.ActiveTool == PreviewTool.None && CurrentState() is { } s
            && OverlayRenderer.ScaleBarBounds(s, FrameSize, BarScale) is { } box && box.Contains(p))
        {
            _dragOffset = p - box.TopLeft;
            CaptureMouse();
            e.Handled = true; // не панорамировать
        }
    }

    /// <summary>Клик линейки/калибровки в координатах кадра (уже ограничен кадром).</summary>
    private void ToolClick(Point p)
    {
        if (_vm is null) return;
        if (_start is not { } a)
        {
            _start = p;
            _end = p;
            Redraw();
            return;
        }
        var b = Snap(p);
        _start = null;
        Redraw();
        if (_vm.ActiveTool == PreviewTool.Ruler) _vm.MeasurePanel.Add(new Measurement(a, b));
        else _ = _vm.ScalePanel.CompleteCalibrationAsync(a, b);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_vm is null) return;
        var p = e.GetPosition(this);

        if (_start is not null)
        {
            _end = Snap(Clamp(p));
            Redraw();
            return;
        }

        if (_dragOffset is { } off && CurrentState() is { } s && OverlayRenderer.ScaleBarBounds(s, FrameSize, BarScale) is { } box)
        {
            var f = FrameSize;
            var x = Math.Clamp(p.X - off.X, 0, Math.Max(0, f.Width - box.Width));
            var y = Math.Clamp(p.Y - off.Y, 0, Math.Max(0, f.Height - box.Height));
            _vm.ScalePanel.SetCustomPosition(x / f.Width, y / f.Height, save: false);
            return;
        }

        if (_vm.ActiveTool == PreviewTool.None)
        {
            var over = CurrentState() is { } st && OverlayRenderer.ScaleBarBounds(st, FrameSize, BarScale) is { } b && b.Contains(p);
            Cursor = over ? Cursors.SizeAll : null;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragOffset is null) return;
        _dragOffset = null;
        ReleaseMouseCapture();
        _vm?.SaveSettingsSoon();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_dragOffset is null) return;
        _dragOffset = null;
        _vm?.SaveSettingsSoon();
    }

    /// <summary>Поверхность рисования: размер кадра, рисует OverlayRenderer.</summary>
    private sealed class Surface : FrameworkElement
    {
        private readonly PreviewOverlay _owner;
        public Surface(PreviewOverlay owner) { _owner = owner; IsHitTestVisible = false; }

        protected override void OnRender(DrawingContext dc)
        {
            if (_owner.CurrentState() is not { } s || _owner.FrameSize is not { Width: > 0, Height: > 0 } size) return;
            OverlayRenderer.Draw(dc, s, size, OverlayRenderer.PreviewScale(size), _owner.Scale);
        }
    }
}

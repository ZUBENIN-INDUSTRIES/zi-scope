using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumeneraViewer.Camera;
using Microsoft.Win32;

namespace LumeneraViewer;

public partial class MainWindow : Window
{
    private readonly ICamera _camera = CameraFactory.IsDriverAvailable() ? CameraFactory.CreateReal(new LumeneraViewer.Settings.CameraSettings()) : CameraFactory.CreateSimulated();
    private readonly object _frameLock = new();
    private CameraFrame? _latestFrame;
    private bool _hasNewFrame;
    private WriteableBitmap? _bitmap;

    public MainWindow()
    {
        InitializeComponent();

        OutputFolderTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "LumeneraCaptures");

        _camera.FrameReceived += OnCameraFrameReceived;
        _camera.Error += message => Dispatcher.BeginInvoke(() => StatusText.Text = message);

        Loaded += (_, _) => ConnectCamera();
        Closing += (_, _) => _camera.Dispose();
        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    private void ConnectCamera()
    {
        try
        {
            _camera.Close();
            _camera.Open();
            _camera.Start();
            StatusText.Text = $"Подключено: {_camera.Info?.Width}x{_camera.Info?.Height}, поток запущен.";
        }
        catch (DllNotFoundException)
        {
            StatusText.Text = "Не найден lucam.dll. Установите драйверы и SDK для Lumenera INFINITY.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ошибка подключения: {ex.Message}";
        }
    }

    private void OnCameraFrameReceived(CameraFrame frame)
    {
        lock (_frameLock)
        {
            _latestFrame = frame;
            _hasNewFrame = true;
        }
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        CameraFrame? frame = null;
        lock (_frameLock)
        {
            if (_hasNewFrame)
            {
                frame = _latestFrame;
                _hasNewFrame = false;
            }
        }

        if (frame is null)
            return;

        if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null);
            PreviewImage.Source = _bitmap;
        }

        var rect = new Int32Rect(0, 0, frame.Width, frame.Height);
        _bitmap.WritePixels(rect, frame.Bgr24, frame.Width * 3, 0);
    }

    private void ShotButton_Click(object sender, RoutedEventArgs e)
    {
        CameraFrame? frame;
        lock (_frameLock)
        {
            frame = _latestFrame;
        }

        if (frame is null)
        {
            StatusText.Text = "Нет кадра для сохранения — поток ещё не запущен.";
            return;
        }

        try
        {
            var folder = OutputFolderTextBox.Text;
            Directory.CreateDirectory(folder);

            var format = (FormatComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "png";
            var fileName = $"Lumenera_{DateTime.Now:yyyyMMdd_HHmmss_fff}.{format}";
            var path = Path.Combine(folder, fileName);

            var source = BitmapSource.Create(
                frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null, frame.Bgr24, frame.Width * 3);

            BitmapEncoder encoder = format switch
            {
                "jpg" => new JpegBitmapEncoder { QualityLevel = 100 },
                "tif" => new TiffBitmapEncoder { Compression = TiffCompressOption.None },
                "bmp" => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder(),
            };
            encoder.Frames.Add(BitmapFrame.Create(source));

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }

            StatusText.Text = $"Сохранено: {path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(OutputFolderTextBox.Text) ? OutputFolderTextBox.Text : null,
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolderTextBox.Text = dialog.FolderName;
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = OutputFolderTextBox.Text;
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true,
        });
    }

    private void ReconnectButton_Click(object sender, RoutedEventArgs e) => ConnectCamera();
}

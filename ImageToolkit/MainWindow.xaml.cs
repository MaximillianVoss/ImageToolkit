using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageToolkit.Core;
using Microsoft.Win32;

namespace ImageToolkit;

public partial class MainWindow : Window
{
    private readonly ImageProcessor _imageProcessor = new();
    private readonly IReadOnlyList<ImageOperationDescriptor> _operations = ImageOperationsCatalog.All;

    private ImageFrame? _originalFrame;
    private ImageFrame? _currentFrame;
    private ImageFrame? _previewFrame;
    private string? _loadedFilePath;

    public MainWindow()
    {
        InitializeComponent();

        OperationComboBox.ItemsSource = _operations;
        OperationComboBox.DisplayMemberPath = nameof(ImageOperationDescriptor.DisplayName);
        OperationComboBox.SelectedIndex = 0;

        UpdateOperationControls(resetSliderValue: true);
        UpdateUiState();
        UpdatePlaceholderVisibility();
        UpdateImageDetails();
        StatusTextBlock.Text = "Откройте изображение, чтобы начать обработку.";
    }

    private void LoadImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите изображение",
            Filter = "Все файлы|*.*|Файлы изображений|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var bitmap = LoadBitmap(dialog.FileName);
            _originalFrame = ConvertToFrame(bitmap);
            _currentFrame = _originalFrame.Clone();
            _loadedFilePath = dialog.FileName;

            OriginalImage.Source = ConvertToBitmapSource(_originalFrame);
            OperationComboBox.SelectedIndex = 0;
            RefreshPreview();
            UpdateUiState();

            StatusTextBlock.Text = "Изображение загружено. Выберите операцию и сохраните результат при необходимости.";
        }
        catch (Exception exception)
        {
            ShowError("Не удалось открыть изображение.", exception);
        }
    }

    private void ApplyOperationButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedOperation = GetSelectedOperation();
        if (_currentFrame is null || _previewFrame is null || selectedOperation.Type == ImageOperationType.None)
        {
            return;
        }

        _currentFrame = _previewFrame;
        OperationComboBox.SelectedIndex = 0;
        RefreshPreview();
        StatusTextBlock.Text = $"Операция «{selectedOperation.DisplayName}» применена к рабочей копии.";
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_originalFrame is null)
        {
            return;
        }

        _currentFrame = _originalFrame.Clone();
        OperationComboBox.SelectedIndex = 0;
        RefreshPreview();
        StatusTextBlock.Text = "Рабочая копия сброшена к исходному изображению.";
    }

    private void SaveImageButton_Click(object sender, RoutedEventArgs e)
    {
        var frameToSave = _previewFrame ?? _currentFrame;
        if (frameToSave is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Сохранить результат",
            FileName = BuildDefaultFileName(),
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|BMP (*.bmp)|*.bmp"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var bitmap = ConvertToBitmapSource(frameToSave);
            var encoder = CreateEncoder(dialog.FileName);
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            StatusTextBlock.Text = $"Результат сохранен в файл: {dialog.FileName}";
        }
        catch (Exception exception)
        {
            ShowError("Не удалось сохранить изображение.", exception);
        }
    }

    private void OperationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateOperationControls(resetSliderValue: true);
        RefreshPreview();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || !GetSelectedOperation().SupportsParameter)
        {
            return;
        }

        ParameterValueTextBlock.Text = Math.Round(ParameterSlider.Value).ToString("0");
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_currentFrame is null)
        {
            _previewFrame = null;
            ResultImage.Source = null;
            UpdateImageDetails();
            UpdatePlaceholderVisibility();
            UpdateUiState();
            return;
        }

        var operation = GetSelectedOperation();
        var parameter = operation.SupportsParameter ? (int)Math.Round(ParameterSlider.Value) : 0;
        var request = new ImageOperationRequest(operation.Type, parameter);
        _previewFrame = _imageProcessor.Apply(_currentFrame, request);
        ResultImage.Source = ConvertToBitmapSource(_previewFrame);

        if (operation.Type != ImageOperationType.None)
        {
            StatusTextBlock.Text = $"Предпросмотр: {operation.DisplayName}. При необходимости нажмите «Применить операцию».";
        }

        UpdateImageDetails();
        UpdatePlaceholderVisibility();
        UpdateUiState();
    }

    private void UpdateOperationControls(bool resetSliderValue)
    {
        var operation = GetSelectedOperation();

        OperationDescriptionTextBlock.Text = operation.Description;
        ParameterNameTextBlock.Text = operation.SupportsParameter ? operation.ParameterLabel : "Параметр";
        ParameterHintTextBlock.Text = operation.ParameterHint;
        ParameterSlider.IsEnabled = operation.SupportsParameter;

        if (operation.SupportsParameter)
        {
            ParameterSlider.Minimum = operation.MinimumParameter;
            ParameterSlider.Maximum = operation.MaximumParameter;
            ParameterSlider.TickFrequency = 1;

            if (resetSliderValue)
            {
                ParameterSlider.Value = operation.DefaultParameter;
            }
        }
        else
        {
            ParameterSlider.Minimum = 0;
            ParameterSlider.Maximum = 0;
            ParameterSlider.Value = 0;
        }

        ParameterValueTextBlock.Text = operation.SupportsParameter
            ? Math.Round(ParameterSlider.Value).ToString("0")
            : "n/a";
    }

    private void UpdateImageDetails()
    {
        if (_originalFrame is null || _currentFrame is null)
        {
            ImageInfoTextBlock.Text = "После открытия файла здесь появятся размеры изображения и статус рабочей копии.";
            return;
        }

        var preview = _previewFrame ?? _currentFrame;
        ImageInfoTextBlock.Text =
            $"Исходник: {_originalFrame.Width} x {_originalFrame.Height}{Environment.NewLine}" +
            $"Рабочая копия: {_currentFrame.Width} x {_currentFrame.Height}{Environment.NewLine}" +
            $"Предпросмотр: {preview.Width} x {preview.Height}";
    }

    private void UpdatePlaceholderVisibility()
    {
        OriginalPlaceholderTextBlock.Visibility = OriginalImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
        ResultPlaceholderTextBlock.Visibility = ResultImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateUiState()
    {
        var hasImage = _currentFrame is not null;
        var selectedOperation = GetSelectedOperation();

        ApplyOperationButton.IsEnabled = hasImage && selectedOperation.Type != ImageOperationType.None;
        ResetButton.IsEnabled = hasImage;
        SaveImageButton.IsEnabled = hasImage;
    }

    private ImageOperationDescriptor GetSelectedOperation()
    {
        return OperationComboBox.SelectedItem as ImageOperationDescriptor ?? _operations[0];
    }

    private string BuildDefaultFileName()
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath))
        {
            return "processed-image.png";
        }

        return $"{Path.GetFileNameWithoutExtension(_loadedFilePath)}-processed.png";
    }

    private static BitmapSource LoadBitmap(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    private static ImageFrame ConvertToFrame(BitmapSource source)
    {
        var bitmap = EnsureBgra32(source);
        var stride = bitmap.PixelWidth * ImageFrame.BytesPerPixel;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        return new ImageFrame(bitmap.PixelWidth, bitmap.PixelHeight, pixels);
    }

    private static BitmapSource ConvertToBitmapSource(ImageFrame frame)
    {
        var bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            frame.CopyPixels(),
            frame.Width * ImageFrame.BytesPerPixel);

        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource EnsureBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static BitmapEncoder CreateEncoder(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 92 },
            ".bmp" => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }

    private void ShowError(string title, Exception exception)
    {
        StatusTextBlock.Text = $"{title} {exception.Message}";
        MessageBox.Show(this, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

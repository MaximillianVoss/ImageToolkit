using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageToolkit.Core;
using Microsoft.Win32;

namespace ImageToolkit;

public partial class MainWindow : Window
{
    private readonly ImageProcessor _imageProcessor = new();
    private readonly OpenAiObjectRecognitionService _objectRecognitionService = new();
    private readonly IReadOnlyList<ImageOperationDescriptor> _operations = ImageOperationsCatalog.All;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private ImageFrame? _originalFrame;
    private ImageFrame? _currentFrame;
    private ImageFrame? _previewFrame;
    private ImageRecognitionResult? _lastRecognitionResult;
    private bool _isRecognizingObjects;
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
        RecognitionResultTextBox.Text = "После распознавания здесь появится отсортированный список объектов и краткое описание сцены.";
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
            LoadImageFromPath(dialog.FileName);
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

    private async void RecognizeObjectsButton_Click(object sender, RoutedEventArgs e)
    {
        var frameToAnalyze = _previewFrame ?? _currentFrame;
        if (frameToAnalyze is null || _isRecognizingObjects)
        {
            return;
        }

        if (!EnsureApiKeyConfigured())
        {
            StatusTextBlock.Text = "Распознавание отменено: OpenAI API key не задан.";
            return;
        }

        _isRecognizingObjects = true;
        UpdateUiState();
        StatusTextBlock.Text = "Выполняется распознавание объектов через OpenAI API...";

        try
        {
            var pngBytes = EncodeFrameAsPng(frameToAnalyze);
            var recognitionResult = await _objectRecognitionService.RecognizeAsync(pngBytes, "image/png");
            _lastRecognitionResult = recognitionResult;

            RecognitionResultTextBox.Text = RecognitionReportFormatter.FormatText(recognitionResult);
            var savedFilePath = SaveRecognitionResult(recognitionResult);

            StatusTextBlock.Text = savedFilePath is null
                ? $"Распознавание завершено. Найдено объектов: {recognitionResult.Objects.Count}. Сохранение отменено."
                : $"Распознавание завершено. Найдено объектов: {recognitionResult.Objects.Count}. Файл сохранен: {savedFilePath}";
        }
        catch (Exception exception)
        {
            ShowError("Не удалось распознать объекты на изображении.", exception);
        }
        finally
        {
            _isRecognizingObjects = false;
            UpdateUiState();
        }
    }

    private void ConfigureApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var diagnosticMessage = OpenAiApiKeyResolver.TryResolve(out _, out var errorMessage)
            ? "Найден действующий источник ключа. При сохранении нового значения он заменит локальный зашифрованный файл для этого пользователя."
            : errorMessage;

        try
        {
            if (PromptForApiKey(diagnosticMessage, out var saveMode))
            {
                StatusTextBlock.Text = saveMode == ApiKeySaveMode.CurrentUser
                    ? "OpenAI API key сохранен локально для текущего пользователя."
                    : "OpenAI API key сохранен только для текущего запуска приложения.";
            }
        }
        catch (Exception exception)
        {
            ShowError("Не удалось сохранить OpenAI API key.", exception);
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
        ViewerPlaceholderTextBlock.Visibility = ResultImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateUiState()
    {
        var hasImage = _currentFrame is not null;
        var selectedOperation = GetSelectedOperation();
        var canInteract = !_isRecognizingObjects;

        LoadImageButton.IsEnabled = canInteract;
        OperationComboBox.IsEnabled = hasImage && canInteract;
        ParameterSlider.IsEnabled = hasImage && canInteract && selectedOperation.SupportsParameter;
        ApplyOperationButton.IsEnabled = hasImage && canInteract && selectedOperation.Type != ImageOperationType.None;
        ResetButton.IsEnabled = hasImage && canInteract;
        SaveImageButton.IsEnabled = hasImage && canInteract;
        RecognizeObjectsButton.IsEnabled = hasImage && canInteract;
        ConfigureApiKeyButton.IsEnabled = canInteract;
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

    public async Task CaptureDocumentationScreenshotsAsync(
        string sampleImagePath,
        string initialScreenshotPath,
        string processedScreenshotPath)
    {
        Left = 120;
        Top = 60;
        Width = 1360;
        Height = 840;
        WindowState = WindowState.Normal;
        Activate();

        await WaitForUiIdleAsync();
        SaveWindowScreenshot(initialScreenshotPath);

        LoadImageFromPath(sampleImagePath);
        var sampleRecognitionResult = new ImageRecognitionResult(
            "На изображении видны геометрические фигуры и контрастные цветовые области на светлом фоне.",
            [
                new RecognizedObject("градиент", 1, "цветной фон"),
                new RecognizedObject("круг", 1, "контур темно-синего цвета"),
                new RecognizedObject("прямоугольник", 2, "контрастные геометрические элементы"),
            ],
            string.Empty);

        _lastRecognitionResult = sampleRecognitionResult.Normalize();
        RecognitionResultTextBox.Text = RecognitionReportFormatter.FormatText(_lastRecognitionResult);
        StatusTextBlock.Text = "Демонстрационный режим: показан пример результата распознавания объектов.";

        await WaitForUiIdleAsync();
        SaveWindowScreenshot(processedScreenshotPath);
    }

    private void LoadImageFromPath(string filePath)
    {
        var bitmap = LoadBitmap(filePath);
        _originalFrame = ConvertToFrame(bitmap);
        _currentFrame = _originalFrame.Clone();
        _loadedFilePath = filePath;
        _lastRecognitionResult = null;

        OperationComboBox.SelectedIndex = 0;
        RefreshPreview();
        RecognitionResultTextBox.Text = "Изображение загружено. При необходимости нажмите «Распознать объекты».";
        UpdateUiState();

        StatusTextBlock.Text = "Изображение загружено. Выберите операцию и сохраните результат при необходимости.";
    }

    private async Task WaitForUiIdleAsync()
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        UpdateLayout();
        await Task.Delay(400);
    }

    private void SaveWindowScreenshot(string filePath)
    {
        var bounds = new Rect(RenderSize);
        var renderTarget = new RenderTargetBitmap(
            (int)Math.Max(1, bounds.Width),
            (int)Math.Max(1, bounds.Height),
            96,
            96,
            PixelFormats.Pbgra32);

        renderTarget.Render(this);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    private string? SaveRecognitionResult(ImageRecognitionResult result)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Сохранить распознанные объекты",
            FileName = BuildDefaultRecognitionFileName(),
            Filter = "Текстовый отчет (*.txt)|*.txt|JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return null;
        }

        var normalized = result.Normalize();
        var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        var content = extension == ".json"
            ? JsonSerializer.Serialize(normalized, JsonSerializerOptions)
            : RecognitionReportFormatter.FormatText(normalized);

        File.WriteAllText(dialog.FileName, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return dialog.FileName;
    }

    private string BuildDefaultRecognitionFileName()
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath))
        {
            return "recognized-objects.txt";
        }

        return $"{Path.GetFileNameWithoutExtension(_loadedFilePath)}-objects.txt";
    }

    private static byte[] EncodeFrameAsPng(ImageFrame frame)
    {
        var bitmap = ConvertToBitmapSource(frame);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private bool EnsureApiKeyConfigured()
    {
        if (OpenAiApiKeyResolver.TryResolve(out _, out _))
        {
            return true;
        }

        try
        {
            return PromptForApiKey(
                "OpenAI API key не найден. Введите ключ, чтобы распознавать объекты на изображениях.",
                out _);
        }
        catch (Exception exception)
        {
            ShowError("Не удалось сохранить OpenAI API key.", exception);
            return false;
        }
    }

    private bool PromptForApiKey(string? diagnosticMessage, out ApiKeySaveMode saveMode)
    {
        saveMode = ApiKeySaveMode.ProcessOnly;

        var dialog = new ApiKeyPromptWindow(diagnosticMessage)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var apiKey = dialog.ApiKey;
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", apiKey);

        if (!dialog.SaveLocally)
        {
            saveMode = ApiKeySaveMode.ProcessOnly;
            return true;
        }

        OpenAiApiKeyResolver.SaveForCurrentUser(apiKey);
        saveMode = ApiKeySaveMode.CurrentUser;
        return true;
    }

    private void ShowError(string title, Exception exception)
    {
        StatusTextBlock.Text = $"{title} {exception.Message}";
        MessageBox.Show(this, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private enum ApiKeySaveMode
    {
        ProcessOnly,
        CurrentUser
    }
}

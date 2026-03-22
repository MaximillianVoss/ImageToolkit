using System.Windows;

namespace ImageToolkit;

public partial class ApiKeyPromptWindow : Window
{
    public ApiKeyPromptWindow(string? diagnosticMessage = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(diagnosticMessage))
        {
            DiagnosticTextBlock.Text = diagnosticMessage;
            DiagnosticTextBlock.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => ApiKeyTextBox.Focus();
    }

    public string ApiKey => ApiKeyTextBox.Text.Trim();

    public bool SaveLocally => SaveLocallyCheckBox.IsChecked != false;

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKeyTextBox.Text))
        {
            MessageBox.Show(this, "Введите OpenAI API key.", "Пустой ключ", MessageBoxButton.OK, MessageBoxImage.Warning);
            ApiKeyTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

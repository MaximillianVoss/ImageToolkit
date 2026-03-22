using System.Text;

namespace ImageToolkit.Core;

public static class RecognitionReportFormatter
{
    public static string FormatText(ImageRecognitionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var normalized = result.Normalize();
        var builder = new StringBuilder();

        builder.AppendLine("Результат распознавания объектов");
        builder.AppendLine($"Дата: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(normalized.SceneSummary))
        {
            builder.AppendLine("Описание сцены:");
            builder.AppendLine(normalized.SceneSummary);
            builder.AppendLine();
        }

        builder.AppendLine("Объекты (отсортированы по имени):");
        if (normalized.Objects.Count == 0)
        {
            builder.AppendLine("- Не удалось выделить объекты.");
        }
        else
        {
            foreach (var item in normalized.Objects)
            {
                builder.Append("- ");
                builder.Append(item.Name);
                builder.Append(" (");
                builder.Append(item.Count);
                builder.Append(')');

                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    builder.Append(": ");
                    builder.Append(item.Description);
                }

                builder.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(normalized.ExtractedText))
        {
            builder.AppendLine();
            builder.AppendLine("Распознанный текст:");
            builder.AppendLine(normalized.ExtractedText);
        }

        return builder.ToString().TrimEnd();
    }
}

namespace ImageToolkit.Core;

public sealed record ImageRecognitionResult(string SceneSummary, IReadOnlyList<RecognizedObject> Objects, string ExtractedText = "")
{
    public ImageRecognitionResult Normalize()
    {
        var mergedObjects = (Objects ?? [])
            .Select(static item => item.Normalize())
            .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group =>
            {
                var preferredName = group
                    .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .First()
                    .Name;
                var mergedDescription = string.Join(
                    "; ",
                    group.Select(static item => item.Description)
                        .Where(static description => !string.IsNullOrWhiteSpace(description))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                return new RecognizedObject(preferredName, group.Sum(static item => item.Count), mergedDescription);
            })
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ImageRecognitionResult(
            (SceneSummary ?? string.Empty).Trim(),
            mergedObjects,
            (ExtractedText ?? string.Empty).Trim());
    }
}

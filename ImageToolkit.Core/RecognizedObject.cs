namespace ImageToolkit.Core;

public sealed record RecognizedObject(string Name, int Count = 1, string Description = "")
{
    public RecognizedObject Normalize()
    {
        var normalizedName = (Name ?? string.Empty).Trim();
        var normalizedDescription = (Description ?? string.Empty).Trim();

        return new RecognizedObject(normalizedName, Math.Max(1, Count), normalizedDescription);
    }
}

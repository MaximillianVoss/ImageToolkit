namespace ImageToolkit.Core;

public sealed record ImageOperationDescriptor(
    ImageOperationType Type,
    string DisplayName,
    string Description,
    bool SupportsParameter = false,
    int MinimumParameter = 0,
    int MaximumParameter = 0,
    int DefaultParameter = 0,
    string ParameterLabel = "")
{
    public string ParameterHint =>
        SupportsParameter
            ? $"{ParameterLabel}: от {MinimumParameter} до {MaximumParameter}"
            : "Для этой операции дополнительный параметр не требуется.";
}

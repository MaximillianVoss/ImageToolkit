namespace ImageToolkit.Core;

public readonly record struct ImageOperationRequest(ImageOperationType Type, int Parameter = 0);

namespace ImageToolkit.Core;

public static class ImageOperationsCatalog
{
    public static IReadOnlyList<ImageOperationDescriptor> All { get; } =
    [
        new(
            ImageOperationType.None,
            "Без изменений",
            "Показывает текущее состояние изображения без дополнительной обработки."),
        new(
            ImageOperationType.Grayscale,
            "Оттенки серого",
            "Преобразует изображение в монохромное по формуле яркости."),
        new(
            ImageOperationType.Invert,
            "Инверсия",
            "Меняет каждый цветовой канал на противоположный."),
        new(
            ImageOperationType.Brightness,
            "Яркость",
            "Увеличивает или уменьшает яркость всех пикселей.",
            SupportsParameter: true,
            MinimumParameter: -100,
            MaximumParameter: 100,
            DefaultParameter: 15,
            ParameterLabel: "Смещение"),
        new(
            ImageOperationType.Threshold,
            "Пороговая обработка",
            "Оставляет только черные и белые пиксели по заданному порогу.",
            SupportsParameter: true,
            MinimumParameter: 0,
            MaximumParameter: 255,
            DefaultParameter: 128,
            ParameterLabel: "Порог"),
        new(
            ImageOperationType.MirrorHorizontal,
            "Зеркало по горизонтали",
            "Отражает изображение слева направо."),
        new(
            ImageOperationType.RotateRight,
            "Поворот вправо",
            "Поворачивает изображение на 90 градусов по часовой стрелке.")
    ];
}

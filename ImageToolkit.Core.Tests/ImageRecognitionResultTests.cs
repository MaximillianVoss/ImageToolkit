using System.Text.RegularExpressions;
using ImageToolkit.Core;

namespace ImageToolkit.Core.Tests;

public sealed class ImageRecognitionResultTests
{
    [Fact]
    public void Normalize_MergesDuplicatesAndSortsByName()
    {
        var result = new ImageRecognitionResult(
            "  City street  ",
            [
                new RecognizedObject("Dog", 1, "brown"),
                new RecognizedObject("apple", 2, "green"),
                new RecognizedObject(" dog ", 3, "sleeping"),
                new RecognizedObject(" ", 4, "ignored")
            ],
            "  SALE  ");

        var normalized = result.Normalize();

        Assert.Equal("City street", normalized.SceneSummary);
        Assert.Equal("SALE", normalized.ExtractedText);
        Assert.Collection(
            normalized.Objects,
            item =>
            {
                Assert.Equal("apple", item.Name);
                Assert.Equal(2, item.Count);
                Assert.Equal("green", item.Description);
            },
            item =>
            {
                Assert.Equal("Dog", item.Name);
                Assert.Equal(4, item.Count);
                Assert.Equal("brown; sleeping", item.Description);
            });
    }

    [Fact]
    public void FormatText_IncludesSortedObjectsAndExtractedText()
    {
        var result = new ImageRecognitionResult(
            "Street market",
            [
                new RecognizedObject("banana", 1),
                new RecognizedObject("apple", 2, "red")
            ],
            "OPEN");

        var report = RecognitionReportFormatter.FormatText(result);

        Assert.Contains("Описание сцены:", report);
        Assert.Contains("Street market", report);
        Assert.Matches(new Regex(@"- apple \(2\): red.*- banana \(1\)", RegexOptions.Singleline), report);
        Assert.Contains("Распознанный текст:", report);
        Assert.Contains("OPEN", report);
    }
}

namespace ApaFlowRecorder.Core.Models;

public sealed class ElementSnapshot
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? TagName { get; set; }
    public string? InputType { get; set; }
    public string? Label { get; set; }
    public string? Text { get; set; }
    public string? Placeholder { get; set; }
    public string? AriaLabel { get; set; }
    public string? CssSelector { get; set; }
    public List<string> AlternateSelectors { get; set; } = [];

    public string DisplayName =>
        FirstNonBlank(Label, AriaLabel, Placeholder, Text, Name, CssSelector, TagName) ?? "目标元素";

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}


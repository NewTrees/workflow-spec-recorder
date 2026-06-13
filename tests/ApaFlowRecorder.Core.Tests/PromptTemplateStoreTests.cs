using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public sealed class PromptTemplateStoreTests
{
    [Fact]
    public void LoadOrCreateDefault_creates_editable_template_file_when_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "prompt-template.md");
        var store = new PromptTemplateStore(path);

        var template = store.LoadOrCreateDefault();

        Assert.True(File.Exists(path));
        Assert.Contains("{{recorded_example}}", template);
        Assert.Contains("{{source_materials}}", template);
        Assert.Contains("{{extra_instruction}}", template);
    }

    [Fact]
    public void ResetToDefault_overwrites_custom_template()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "prompt-template.md");
        var store = new PromptTemplateStore(path);
        store.Save("custom");

        var template = store.ResetToDefault();

        Assert.Equal(template, File.ReadAllText(path));
        Assert.Contains(GeneralizedRequirementPromptBuilder.RequirementDocumentTemplatePlaceholder, template);
        Assert.DoesNotContain("人民币汇率中间价自动采集", template);
    }
}

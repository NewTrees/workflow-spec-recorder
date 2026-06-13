using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public sealed class RequirementDocumentTemplateStoreTests
{
    [Fact]
    public void Default_template_uses_empty_apa_creator_structure_without_example_business_content()
    {
        var template = RequirementDocumentTemplateStore.DefaultTemplate;

        Assert.StartsWith("# [流程名称]", template);
        Assert.Contains("## 一、流程概述", template);
        Assert.Contains("## 二、输入与输出", template);
        Assert.Contains("## 三、操作步骤（按执行顺序编号）", template);
        Assert.Contains("| 参数名 | 类型 | 必填 | 默认值 | 说明 |", template);
        Assert.Contains("| 异常情况 | 处理方式 |", template);
        Assert.DoesNotContain("人民币汇率中间价自动采集", template);
        Assert.DoesNotContain("中国人民银行", template);
        Assert.DoesNotContain("为什么这个格式最好", template);
        Assert.DoesNotContain("反例", template);
    }

    [Fact]
    public void ResetToDefault_writes_editable_document_template_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "requirement-document-template.md");
        var store = new RequirementDocumentTemplateStore(path);
        store.Save("custom");

        var template = store.ResetToDefault();

        Assert.Equal(template, File.ReadAllText(path));
        Assert.Contains("# [流程名称]", template);
        Assert.Contains("### 步骤1：[步骤标题]", template);
    }
}

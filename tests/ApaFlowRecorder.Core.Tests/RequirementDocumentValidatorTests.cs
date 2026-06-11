using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public sealed class RequirementDocumentValidatorTests
{
    [Fact]
    public void Validate_accepts_required_apa_requirement_template()
    {
        var markdown = """
        # 项目需求描述

        ## 项目目标
        目标。

        ## 流程步骤
        第一步：处理。

        ## 流程输入
        1. 输入。

        ## 流程输出
        1. 输出。

        ## 约束与异常处理
        1. 异常记录。
        """;

        var result = RequirementDocumentValidator.Validate(markdown);

        Assert.True(result.IsValid);
        Assert.Empty(result.MissingRequiredSections);
        Assert.Empty(result.ForbiddenSections);
    }

    [Fact]
    public void Validate_rejects_analysis_report_sections_and_missing_template_sections()
    {
        var markdown = """
        # 需求分析报告

        ## 录制示例如何参与泛化
        示例说明。

        ```mermaid
        graph TD
        ```
        """;

        var result = RequirementDocumentValidator.Validate(markdown);

        Assert.False(result.IsValid);
        Assert.Contains("# 项目需求描述", result.MissingRequiredSections);
        Assert.Contains("录制示例如何参与泛化", result.ForbiddenSections);
        Assert.Contains("```mermaid", result.ForbiddenSections);
    }
}

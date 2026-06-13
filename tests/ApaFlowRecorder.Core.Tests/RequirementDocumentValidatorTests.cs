using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public sealed class RequirementDocumentValidatorTests
{
    [Fact]
    public void Validate_accepts_required_apa_requirement_template()
    {
        var markdown = """
        # 报表整理流程

        ## 一、流程概述

        - **目标**：整理报表。
        - **触发方式**：手动运行。
        - **最终产出**：Excel 文件和日志。

        ## 二、输入与输出

        ### 输入

        | 参数名 | 类型 | 必填 | 默认值 | 说明 |
        |--------|------|------|--------|------|
        | input_file | file | 是 | "" | 输入文件 |

        ### 输出

        | 输出项 | 格式 | 说明 |
        |--------|------|------|
        | 报表 | Excel | 结果文件 |

        ## 三、操作步骤（按执行顺序编号）

        ### 步骤1：读取资料
        - **操作对象**：输入文件。
        - **具体动作**：读取并校验文件。
        - **关键元素**：表头。
        - **预期结果**：得到记录集合。
        - **失败处理**：记录错误并中止。

        ## 四、业务规则与约束

        - 规则1：文件名必须包含日期。

        ## 五、异常场景

        | 异常情况 | 处理方式 |
        |----------|----------|
        | 数据为空 | 记录警告 |

        ## 六、参考信息（可选但非常有帮助）

        - **截图**：无。
        - **示例数据**：待补充。
        - **已有账号/凭据**：无。
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
        Assert.Contains("## 一、流程概述", result.MissingRequiredSections);
        Assert.Contains("录制示例如何参与泛化", result.ForbiddenSections);
        Assert.Contains("```mermaid", result.ForbiddenSections);
    }

    [Fact]
    public void Validate_uses_custom_requirement_document_template_when_provided()
    {
        var template = """
        # [流程名称]

        ## 自定义输入

        | 字段 | 说明 |
        |------|------|
        | xxx | xxx |

        ## 自定义输出
        """;
        var markdown = """
        # 流程A

        ## 自定义输入

        | 字段 | 说明 |
        |------|------|
        | 文件 | 输入文件 |
        """;

        var result = RequirementDocumentValidator.Validate(markdown, template);

        Assert.False(result.IsValid);
        Assert.DoesNotContain("## 一、流程概述", result.MissingRequiredSections);
        Assert.Contains("## 自定义输出", result.MissingRequiredSections);
    }
}

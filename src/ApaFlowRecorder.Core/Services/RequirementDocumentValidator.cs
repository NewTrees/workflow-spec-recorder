namespace ApaFlowRecorder.Core.Services;

public sealed record RequirementDocumentValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingRequiredSections,
    IReadOnlyList<string> ForbiddenSections);

public static class RequirementDocumentValidator
{
    public static readonly string[] RequiredSections =
    [
        "## 一、流程概述",
        "- **目标**：",
        "- **触发方式**：",
        "- **最终产出**：",
        "## 二、输入与输出",
        "### 输入",
        "| 参数名 | 类型 | 必填 | 默认值 | 说明 |",
        "### 输出",
        "| 输出项 | 格式 | 说明 |",
        "## 三、操作步骤（按执行顺序编号）",
        "### 步骤1：",
        "- **操作对象**：",
        "- **具体动作**：",
        "- **关键元素**：",
        "- **预期结果**：",
        "- **失败处理**：",
        "## 四、业务规则与约束",
        "## 五、异常场景",
        "| 异常情况 | 处理方式 |",
        "## 六、参考信息（可选但非常有帮助）"
    ];

    public static readonly string[] ForbiddenSectionFragments =
    [
        "录制示例如何参与泛化",
        "需求推断说明",
        "示例步骤 vs 泛化步骤对照",
        "示例步骤 VS 泛化步骤对照",
        "总体流程图",
        "```mermaid"
    ];

    public static RequirementDocumentValidationResult Validate(string markdown, string? requirementDocumentTemplate = null)
    {
        var content = markdown ?? string.Empty;
        var requiredFragments = BuildRequiredFragments(requirementDocumentTemplate);
        var missing = requiredFragments
            .Where(section => !content.Contains(section, StringComparison.Ordinal))
            .ToList();
        var forbidden = ForbiddenSectionFragments
            .Where(section => content.Contains(section, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new RequirementDocumentValidationResult(
            missing.Count == 0 && forbidden.Count == 0 && content.TrimStart().StartsWith("# ", StringComparison.Ordinal),
            missing,
            forbidden);
    }

    public static string BuildRepairPrompt(
        string invalidMarkdown,
        RequirementDocumentValidationResult validationResult,
        string? requirementDocumentTemplate = null)
    {
        var missing = validationResult.MissingRequiredSections.Count == 0
            ? "无"
            : string.Join("、", validationResult.MissingRequiredSections);
        var forbidden = validationResult.ForbiddenSections.Count == 0
            ? "无"
            : string.Join("、", validationResult.ForbiddenSections);

        return $"""
        下面是一份不合格的 APA 需求文档，请只根据原文内容重写为合格 Markdown，不要新增分析过程。

        必须满足：
        1. 文档必须以 `# [流程名称]` 格式的一级标题开头，标题要替换为真实流程名称。
        2. 必须按下面的“需求文档模板”填充，不要输出模板说明、示例讲解或分析过程。
        3. 如果模板中存在表格、步骤字段或章节标题，修正文档必须保留对应结构并填入真实业务内容。
        4. 不要把模板中的 `xxx`、`[步骤标题]`、`...` 这类占位内容原样保留在最终文档中。
        5. 不要输出“录制示例如何参与泛化”“需求推断说明”“示例步骤 vs 泛化步骤对照”“总体流程图”等分析章节。
        6. 不要输出 Mermaid、ASCII 流程图或伪代码大段代码块。
        7. 直接输出修正后的 Markdown，不要解释。

        当前缺失章节：{missing}
        当前禁止内容：{forbidden}

        # 需求文档模板
        {NormalizeTemplate(requirementDocumentTemplate)}

        # 待修正文档
        {invalidMarkdown}
        """;
    }

    private static IReadOnlyList<string> BuildRequiredFragments(string? requirementDocumentTemplate)
    {
        if (string.IsNullOrWhiteSpace(requirementDocumentTemplate))
        {
            return RequiredSections;
        }

        var fragments = requirementDocumentTemplate
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(IsRequiredTemplateLine)
            .Select(NormalizeTemplateRequiredLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return fragments.Count == 0 ? RequiredSections : fragments;
    }

    private static bool IsRequiredTemplateLine(string line)
    {
        if (line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal))
        {
            return true;
        }

        if (line.StartsWith("| ", StringComparison.Ordinal)
            && !line.Contains("---", StringComparison.Ordinal)
            && !line.Contains("xxx", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return line.StartsWith("- **", StringComparison.Ordinal);
    }

    private static string NormalizeTemplateRequiredLine(string line)
    {
        if (line.StartsWith("- **", StringComparison.Ordinal))
        {
            var colonIndex = line.IndexOf('：');
            if (colonIndex < 0)
            {
                colonIndex = line.IndexOf(':');
            }

            if (colonIndex >= 0)
            {
                return line[..(colonIndex + 1)];
            }
        }

        return line
            .Replace("[步骤标题]", string.Empty, StringComparison.Ordinal)
            .Replace("（同上格式）", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeTemplate(string? requirementDocumentTemplate) =>
        string.IsNullOrWhiteSpace(requirementDocumentTemplate)
            ? RequirementDocumentTemplateStore.DefaultTemplate
            : requirementDocumentTemplate;
}

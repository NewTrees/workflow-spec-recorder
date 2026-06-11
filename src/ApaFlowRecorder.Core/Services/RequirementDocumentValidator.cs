namespace ApaFlowRecorder.Core.Services;

public sealed record RequirementDocumentValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingRequiredSections,
    IReadOnlyList<string> ForbiddenSections);

public static class RequirementDocumentValidator
{
    public static readonly string[] RequiredSections =
    [
        "# 项目需求描述",
        "## 项目目标",
        "## 流程步骤",
        "## 流程输入",
        "## 流程输出",
        "## 约束与异常处理"
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

    public static RequirementDocumentValidationResult Validate(string markdown)
    {
        var content = markdown ?? string.Empty;
        var missing = RequiredSections
            .Where(section => !content.Contains(section, StringComparison.Ordinal))
            .ToList();
        var forbidden = ForbiddenSectionFragments
            .Where(section => content.Contains(section, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new RequirementDocumentValidationResult(
            missing.Count == 0 && forbidden.Count == 0 && content.TrimStart().StartsWith("# 项目需求描述", StringComparison.Ordinal),
            missing,
            forbidden);
    }

    public static string BuildRepairPrompt(string invalidMarkdown, RequirementDocumentValidationResult validationResult)
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
        1. 文档必须以 `# 项目需求描述` 开头。
        2. 必须包含并只围绕这些核心章节展开：`## 项目目标`、`## 流程步骤`、`## 流程输入`、`## 流程输出`、`## 约束与异常处理`。
        3. 不要输出“录制示例如何参与泛化”“需求推断说明”“示例步骤 vs 泛化步骤对照”“总体流程图”等分析章节。
        4. 不要输出 Mermaid、ASCII 流程图或伪代码大段代码块。
        5. 直接输出修正后的 Markdown，不要解释。

        当前缺失章节：{missing}
        当前禁止内容：{forbidden}

        # 待修正文档
        {invalidMarkdown}
        """;
    }
}

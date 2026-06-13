using System.Text;
using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class GeneralizedRequirementPromptBuilder
{
    public const string RecordedExamplePlaceholder = "{{recorded_example}}";
    public const string SourceMaterialsPlaceholder = "{{source_materials}}";
    public const string ExtraInstructionPlaceholder = "{{extra_instruction}}";
    public const string RequirementDocumentTemplatePlaceholder = "{{requirement_document_template}}";

    public const string DefaultTemplate =
        """
        你是资深 RPA / APA 需求分析师。你的任务不是复述录制操作，也不是写测试报告，而是把人类业务需求步骤、粗略需求文档、输入样例、输出样例、参考资料和可选录制示例归纳成给 AI 执行/理解的业务需求文档。

        录制步骤是代表性示例，不是最终自动化流程本身。你必须像人与人沟通需求一样推断完整业务意图：如果用户只演示了一个分类、一个维度、一个标签、一行记录或一个按钮，但资料或页面语义表明存在同类集合，应抽象为遍历全部同类对象。
        如果没有录制步骤，请完全基于输入资料生成文件处理、数据处理或文档处理流程。
        必须输出 Markdown，重点描述业务目标、业务步骤、业务输入、业务输出、循环结构、动态发现规则、字段提取和结果写入。不要把代表性示例里的具体对象、标签、行号或按钮写死为固定步骤。
        禁止把采集过程中的技术细节写进最终文档：浏览器类型、页面 URL、CSS 选择器、XPath、DOM 结构、桌面应用程序标识、窗口句柄、截图路径、录制工具状态等都不要写，除非它本身就是业务系统名称、业务入口或业务输入。

        {{recorded_example}}

        {{source_materials}}

        {{extra_instruction}}

        # 最终文档输出要求
        必须严格填充下面的“需求文档模板”。把 `# [流程名称]` 替换成根据资料推断出的真实流程名称；如果无法推断，使用项目名或“未命名流程”。最终文档要像交付给 APA Creator / APA 开发智能体的需求文档，不要像分析报告、推理笔记或录屏摘要。

        填写质量要求：
        - “最终产出”可以写成多行清单，明确文件、日志、状态码或系统回写。
        - “具体动作”可以用编号子步骤，写清打开、点击、填写、提交、读取、遍历、写入等业务动作。
        - “失败处理”要尽量写清重试次数、间隔，以及跳过、继续、中止或人工确认策略。
        - “输出”表要写清格式和字段说明。
        - “参考信息”可以写目标网址、输出目录、示例数据来源和账号要求，但不要输出真实密码或敏感凭据。

        # 最终需求文档模板
        {{requirement_document_template}}

        禁止事项：不要输出“录制示例如何参与泛化”“需求推断说明”“示例步骤 vs 泛化步骤对照”“总体流程图”等分析过程章节；不要输出 Mermaid、ASCII 流程图、伪代码大段代码块；不要输出浏览器选择器、CSS、XPath、DOM、截图本地路径、桌面窗口句柄等采集技术信息，除非它本身就是业务系统名称、业务入口或业务输入。

        请直接输出最终 Markdown，不要输出解释。
        """;

    public string Build(
        WorkflowSession session,
        SourceMaterialBundle materials,
        string? extraInstruction,
        string? promptTemplate = null,
        string? requirementDocumentTemplate = null)
    {
        var recordedExample = BuildRecordedExample(session);
        var sourceMaterials = BuildSourceMaterials(materials);
        var userInstruction = BuildExtraInstruction(extraInstruction);
        var template = string.IsNullOrWhiteSpace(promptTemplate) ? DefaultTemplate : promptTemplate;
        var documentTemplate = string.IsNullOrWhiteSpace(requirementDocumentTemplate)
            ? RequirementDocumentTemplateStore.DefaultTemplate
            : requirementDocumentTemplate;

        var rendered = template
            .Replace(RecordedExamplePlaceholder, recordedExample, StringComparison.Ordinal)
            .Replace(SourceMaterialsPlaceholder, sourceMaterials, StringComparison.Ordinal)
            .Replace(ExtraInstructionPlaceholder, userInstruction, StringComparison.Ordinal)
            .Replace(RequirementDocumentTemplatePlaceholder, documentTemplate, StringComparison.Ordinal);

        var builder = new StringBuilder(rendered.TrimEnd());
        if (!template.Contains(RecordedExamplePlaceholder, StringComparison.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(recordedExample);
        }

        if (!template.Contains(SourceMaterialsPlaceholder, StringComparison.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine(sourceMaterials);
        }

        if (!template.Contains(ExtraInstructionPlaceholder, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(userInstruction))
        {
            builder.AppendLine();
            builder.AppendLine(userInstruction);
        }

        if (!template.Contains(RequirementDocumentTemplatePlaceholder, StringComparison.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine("# 最终需求文档模板");
            builder.AppendLine(documentTemplate);
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildRecordedExample(WorkflowSession session)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 当前录制示例");
        if (session.Steps.Count == 0)
        {
            builder.AppendLine("无录制步骤。请主要依据输入资料生成流程；这可能是纯 Excel、文档、PPT/PDF 处理流程。");
            return builder.ToString();
        }

        builder.AppendLine("下列步骤是业务操作示例；如显示“截图=已附带”，对应截图会作为多模态图像资料随请求发送。请结合截图中的页面结构、可见文本和业务上下文理解需求。最终文档只写业务流程，不要写浏览器、URL、选择器、截图路径等采集技术细节。");
        foreach (var step in session.Steps.Select((value, index) => new { value, index }))
        {
            var screenshotStatus = HasUsableScreenshot(step.value.ScreenshotPath) ? "已附带" : "无";
            builder.AppendLine($"{step.index + 1}. 步骤标题={step.value.Title}; 页面上下文={step.value.PageTitle}; 操作对象={step.value.Element?.DisplayName}; 录入值={MaskIfNeeded(step.value)}; 截图={screenshotStatus}");
        }

        return builder.ToString();
    }

    private static bool HasUsableScreenshot(string? screenshotPath) =>
        !string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath);

    private static string MaskIfNeeded(RecordedStep step)
    {
        if (step.IsSensitive)
        {
            return "[敏感字段]";
        }

        return string.IsNullOrWhiteSpace(step.LiteralValue) ? "无" : step.LiteralValue;
    }

    private static string BuildSourceMaterials(SourceMaterialBundle materials)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 输入资料清单");
        if (materials.Files.Count == 0 && !materials.Workbooks.Any() && !materials.Documents.Any())
        {
            builder.AppendLine("未提供输入资料。");
            return builder.ToString();
        }

        foreach (var file in materials.Files)
        {
            builder.AppendLine($"- 文件：{file.FileName}; 类型：{file.Kind}; 状态：{file.Status}");
        }
        builder.AppendLine();

        AppendEmbeddedImages(builder, materials.EmbeddedImages);
        AppendWorkbooks(builder, materials.Workbooks);
        AppendDocuments(builder, materials.Documents);
        AppendPresentations(builder, materials.Presentations);
        AppendOtherFiles(builder, materials.Files);
        return builder.ToString();
    }

    private static string BuildExtraInstruction(string? extraInstruction)
    {
        if (string.IsNullOrWhiteSpace(extraInstruction))
        {
            return string.Empty;
        }

        return $"# 用户补充要求{Environment.NewLine}{extraInstruction}{Environment.NewLine}";
    }

    private static void AppendEmbeddedImages(StringBuilder builder, IEnumerable<SourceMaterialImage> embeddedImages)
    {
        var list = embeddedImages.ToList();
        if (list.Count == 0)
        {
            return;
        }

        builder.AppendLine("# Office 内嵌图片视觉证据");
        builder.AppendLine("以下图片来自 Word/PPT 资料正文中的截图或示意图，会随请求作为多模态图片发送给支持图像输入的大模型。生成 APA 需求文档时必须把这些图片与文档文字一起理解。");
        foreach (var image in list)
        {
            builder.AppendLine($"- {image.Label}; 类型={image.MediaType}; 文件={Path.GetFileName(image.Path)}");
        }
        builder.AppendLine();
    }

    private static void AppendWorkbooks(StringBuilder builder, IEnumerable<WorkbookSummary> workbooks)
    {
        var list = workbooks.ToList();
        builder.AppendLine("# Excel 工作簿结构");
        if (list.Count == 0)
        {
            builder.AppendLine("未提供 Excel。");
            builder.AppendLine();
            return;
        }

        foreach (var workbook in list)
        {
            builder.AppendLine($"## 文件：{workbook.FileName}");
            foreach (var sheet in workbook.Worksheets)
            {
                builder.AppendLine($"### Sheet: {sheet.Name}; 行数={sheet.RowCount}; 列数={sheet.ColumnCount}");
                builder.AppendLine($"表头：{string.Join(" | ", sheet.Headers)}");
                foreach (var row in sheet.PreviewRows.Take(5))
                {
                    builder.AppendLine("- " + string.Join(" | ", row));
                }
            }
        }
        builder.AppendLine();
    }

    private static void AppendDocuments(StringBuilder builder, IEnumerable<DocumentSummary> documents)
    {
        var list = documents.ToList();
        builder.AppendLine("# Word / 文本文档说明");
        if (list.Count == 0)
        {
            builder.AppendLine("未提供 Word/TXT/Markdown 文档正文。");
            builder.AppendLine();
            return;
        }

        foreach (var document in list)
        {
            builder.AppendLine($"## 文件：{document.FileName}");
            builder.AppendLine(document.Text);
        }
        builder.AppendLine();
    }

    private static void AppendPresentations(StringBuilder builder, IEnumerable<PresentationSummary> presentations)
    {
        var list = presentations.ToList();
        builder.AppendLine("# PPT 演示文稿内容");
        if (list.Count == 0)
        {
            builder.AppendLine("未提供 PPT。");
            builder.AppendLine();
            return;
        }

        foreach (var presentation in list)
        {
            builder.AppendLine($"## 文件：{presentation.FileName}");
            foreach (var slide in presentation.Slides.Take(30))
            {
                builder.AppendLine($"- Slide {slide.Number}: {slide.Text}");
            }
        }
        builder.AppendLine();
    }

    private static void AppendOtherFiles(StringBuilder builder, IEnumerable<SourceMaterialFile> files)
    {
        var otherFiles = files
            .Where(file => file.Workbook is null && file.Document is null && file.Presentation is null)
            .ToList();
        if (otherFiles.Count == 0)
        {
            return;
        }

        builder.AppendLine("# 其他资料");
        foreach (var file in otherFiles)
        {
            builder.AppendLine($"## 文件：{file.FileName}");
            builder.AppendLine(file.TextPreview);
        }
        builder.AppendLine();
    }
}

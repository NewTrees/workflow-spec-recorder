using System.Text;
using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class GeneralizedRequirementPromptBuilder
{
    public const string RecordedExamplePlaceholder = "{{recorded_example}}";
    public const string SourceMaterialsPlaceholder = "{{source_materials}}";
    public const string ExtraInstructionPlaceholder = "{{extra_instruction}}";

    public const string DefaultTemplate =
        """
        你是资深 RPA / APA 需求分析师。你的任务不是复述录制操作，而是把人类业务需求步骤、粗略需求文档、输入样例、输出样例、参考资料和可选录制示例归纳成可执行的 APA 需求文档。

        录制步骤是代表性示例，不是最终自动化流程本身。你必须像人与人沟通需求一样推断完整业务意图：如果用户只演示了一个分类、一个维度、一个标签、一行记录或一个按钮，但资料或页面语义表明存在同类集合，应抽象为遍历全部同类对象。
        如果没有录制步骤，请完全基于输入资料生成文件处理、数据处理或文档处理流程。
        必须输出 Markdown，并显式描述输入资料、输出成果、循环结构、动态发现规则、字段提取、文件写入、等待/异常/日志策略。不要把代表性示例里的具体对象、标签、行号或按钮写死为固定步骤。

        {{recorded_example}}

        {{source_materials}}

        {{extra_instruction}}

        # 最终文档输出要求
        必须严格输出以下 Markdown 模板，文档标题必须是 `# 项目需求描述`。最终文档要像交付给 APA 开发/执行智能体的需求文档，不要像分析报告、推理笔记或录屏摘要。

        # 项目需求描述

        ## 项目目标
        用 1 到 3 段自然语言说明业务目标、自动化闭环、核心处理对象、输入来源和输出成果。可以补充“本流程目标不是简单网页复制/固定点击，而是完成……的完整自动化闭环”。

        ## 流程步骤
        用“第一步：……、第二步：……”格式描述可执行步骤。每一步必须包含操作对象、处理规则、关键验证方式、失败时如何处理。若存在循环，请在步骤文字里写清楚从哪里读取集合、如何遍历、每轮写入什么结果；可以在自然语言中写明 foreach 输入记录、foreach 动态集合等循环关系。

        ## 流程输入
        用编号列表写清楚输入变量、文件、Sheet、字段、配置项和默认值。未知项可以写成“待确认”，但不要编造不存在的输入。

        ## 流程输出
        用编号列表写清楚输出文件、写入位置、字段结构、状态回写、日志或布尔结果。未知项可以写成“待确认”。

        ## 约束与异常处理
        只保留执行必须知道的等待、异常、重试、限流、断点续跑、数据校验和人工确认项。

        禁止事项：不要输出“录制示例如何参与泛化”“需求推断说明”“示例步骤 vs 泛化步骤对照”“总体流程图”等分析过程章节；不要输出 Mermaid、ASCII 流程图、伪代码大段代码块；不要为了显得完整而堆表格。

        请直接输出最终 Markdown，不要输出解释。
        """;

    public string Build(
        WorkflowSession session,
        SourceMaterialBundle materials,
        string? extraInstruction,
        string? promptTemplate = null)
    {
        var recordedExample = BuildRecordedExample(session);
        var sourceMaterials = BuildSourceMaterials(materials);
        var userInstruction = BuildExtraInstruction(extraInstruction);
        var template = string.IsNullOrWhiteSpace(promptTemplate) ? DefaultTemplate : promptTemplate;

        var rendered = template
            .Replace(RecordedExamplePlaceholder, recordedExample, StringComparison.Ordinal)
            .Replace(SourceMaterialsPlaceholder, sourceMaterials, StringComparison.Ordinal)
            .Replace(ExtraInstructionPlaceholder, userInstruction, StringComparison.Ordinal);

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

        builder.AppendLine("下列步骤如显示“截图=已附带”，对应截图会作为多模态图像资料随请求发送。请结合截图中的页面结构、可见文本、按钮位置和业务上下文理解需求，不要只依赖文字步骤。");
        foreach (var step in session.Steps.Select((value, index) => new { value, index }))
        {
            var screenshotStatus = HasUsableScreenshot(step.value.ScreenshotPath) ? "已附带" : "无";
            builder.AppendLine($"{step.index + 1}. 动作={step.value.Action}; 标题={step.value.Title}; 页面={step.value.PageTitle}; URL={step.value.PageUrl}; 元素={step.value.Element?.DisplayName}; 选择器={step.value.Element?.CssSelector}; 截图={screenshotStatus}");
        }

        return builder.ToString();
    }

    private static bool HasUsableScreenshot(string? screenshotPath) =>
        !string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath);

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

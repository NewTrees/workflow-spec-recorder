using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class GeneralizedRequirementGenerator
{
    private readonly GeneralizedRequirementPromptBuilder _promptBuilder = new();
    private readonly OpenAiCompatibleChatClient _chatClient;

    public GeneralizedRequirementGenerator(OpenAiCompatibleChatClient? chatClient = null)
    {
        _chatClient = chatClient ?? new OpenAiCompatibleChatClient();
    }

    public async Task<GeneralizedRequirementResult> GenerateAsync(
        WorkflowSession session,
        SourceMaterialBundle materials,
        LlmSettings settings,
        string? extraInstruction,
        string? promptTemplate = null,
        string? requirementDocumentTemplate = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = _promptBuilder.Build(session, materials, extraInstruction, promptTemplate, requirementDocumentTemplate);
        var visualAttachments = BuildVisualAttachments(session, materials);
        if (settings.IsConfigured)
        {
            var markdown = await _chatClient.CompleteAsync(settings, prompt, visualAttachments, cancellationToken);
            var validation = RequirementDocumentValidator.Validate(markdown, requirementDocumentTemplate);
            var generationMode = $"LLM: {settings.ProviderName}/{settings.Model}";
            if (!validation.IsValid)
            {
                var repairPrompt = RequirementDocumentValidator.BuildRepairPrompt(markdown, validation, requirementDocumentTemplate);
                markdown = await _chatClient.CompleteAsync(settings, repairPrompt, cancellationToken);
                generationMode += " + template repair";
            }

            return new GeneralizedRequirementResult
            {
                Markdown = markdown,
                SpecJson = BuildSpecJson(session, materials, "llm", visualAttachments),
                GenerationMode = generationMode,
                Prompt = prompt
            };
        }

        return new GeneralizedRequirementResult
        {
            Markdown = GenerateRuleBasedMarkdown(session, materials, extraInstruction),
            SpecJson = BuildSpecJson(session, materials, "rule-based", visualAttachments),
            GenerationMode = "Rule-based fallback",
            Prompt = prompt
        };
    }

    private static IReadOnlyList<LlmImageAttachment> BuildVisualAttachments(WorkflowSession session, SourceMaterialBundle materials)
    {
        var stepImages = session.Steps
            .Select((step, index) => new { step, index })
            .Where(item => !string.IsNullOrWhiteSpace(item.step.ScreenshotPath) && File.Exists(item.step.ScreenshotPath))
            .Select(item => new LlmImageAttachment
            {
                Path = item.step.ScreenshotPath!,
                Label = $"步骤 {item.index + 1}：{item.step.Title}",
                MediaType = GuessImageMediaType(item.step.ScreenshotPath!)
            });

        var materialImages = materials.Files
            .Where(file => IsImageMaterial(file.Path) && File.Exists(file.Path))
            .Select(file => new LlmImageAttachment
            {
                Path = file.Path,
                Label = $"资料图片：{file.FileName}",
                MediaType = GuessImageMediaType(file.Path)
            });

        var embeddedOfficeImages = materials.EmbeddedImages
            .Where(image => File.Exists(image.Path))
            .Select(image => new LlmImageAttachment
            {
                Path = image.Path,
                Label = image.Label,
                MediaType = string.IsNullOrWhiteSpace(image.MediaType) ? GuessImageMediaType(image.Path) : image.MediaType
            });

        return stepImages
            .Concat(materialImages)
            .Concat(embeddedOfficeImages)
            .GroupBy(image => Path.GetFullPath(image.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool IsImageMaterial(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp";
    }

    private static string GuessImageMediaType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    private static string GenerateRuleBasedMarkdown(WorkflowSession session, SourceMaterialBundle materials, string? extraInstruction)
    {
        return GenerateGenericMarkdown(session, materials, extraInstruction);
    }

    private static bool LooksLikeVehicleReputationTask(SourceMaterialBundle materials, string? extraInstruction)
    {
        var workbookText = string.Join("\n", materials.Workbooks.SelectMany(workbook =>
            workbook.Worksheets.SelectMany(sheet => new[] { workbook.FileName, sheet.Name }.Concat(sheet.Headers))));
        var documentText = string.Join("\n", materials.Documents.Select(document => document.Text));
        var presentationText = string.Join("\n", materials.Presentations.SelectMany(presentation => presentation.Slides.Select(slide => slide.Text)));
        var otherText = string.Join("\n", materials.Files.Select(file => $"{file.FileName}\n{file.TextPreview}"));
        var haystack = string.Join("\n", workbookText, documentText, presentationText, otherText, extraInstruction);

        return haystack.Contains("车型", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("懂车帝", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("口碑", StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateVehicleReputationMarkdown(WorkflowSession session, SourceMaterialBundle materials, string? extraInstruction)
    {
        var workbook = materials.Workbooks.FirstOrDefault();
        var masterSheet = workbook?.Worksheets.FirstOrDefault(s => s.Name.Contains("车型", StringComparison.OrdinalIgnoreCase))
            ?? workbook?.Worksheets.FirstOrDefault();
        var modelColumn = GuessModelColumn(masterSheet);
        var stepSummary = session.Steps.Count == 0
            ? "无录制步骤，本流程按输入资料生成泛化流程。"
            : string.Join("；", session.Steps.Take(12).Select((s, i) => $"{i + 1}. {s.Title}"));

        var builder = new StringBuilder();
        builder.AppendLine("# 懂车帝车型口碑数据提取整理 APA 需求文档");
        builder.AppendLine();
        builder.AppendLine("## 1. 项目目标");
        builder.AppendLine("根据输入资料中的车型清单，逐个进入懂车帝搜索并采集车型口碑数据。流程需要动态识别页面上实际存在的口碑维度、评价标签和评价列表，展开完整评价正文后，将评价内容与评价链接写入对应车型 Sheet。录制步骤只作为示例轨迹，不能把示例车型、示例维度或示例标签写死。");
        builder.AppendLine();
        builder.AppendLine("## 2. 输入资料与输出成果");
        builder.AppendLine($"- 输入文件：`{workbook?.FileName ?? "车型清单.xlsx"}`");
        builder.AppendLine($"- 主表：`{masterSheet?.Name ?? "车型清单"}`");
        builder.AppendLine($"- 车型字段：{modelColumn}");
        builder.AppendLine("- 遍历规则：从主表逐行读取非空车型；若存在品牌列，则可将品牌 + 车型拼接为搜索关键词。");
        builder.AppendLine("- 输出字段：序号、平台、维度、优缺点、评价内容、评价链接");
        builder.AppendLine("- 输出规则：每个车型一个 Sheet；若 Sheet 不存在则新建；若已存在则按执行参数选择清理旧结果或追加新结果。");
        builder.AppendLine();
        builder.AppendLine("## 3. 录制示例如何参与泛化");
        builder.AppendLine($"- 本次录制示例：{stepSummary}");
        builder.AppendLine("- 示例只用于说明页面路径和元素结构。APA 实现时必须按输入表和实时 DOM 动态遍历。");
        builder.AppendLine();
        builder.AppendLine("## 4. 总体流程");
        builder.AppendLine("```text");
        builder.AppendLine("读取车型清单");
        builder.AppendLine("foreach 车型 in 车型清单:");
        builder.AppendLine("  打开懂车帝并搜索车型");
        builder.AppendLine("  进入车型详情页");
        builder.AppendLine("  进入口碑页");
        builder.AppendLine("  动态识别当前车型实际存在的全部口碑维度");
        builder.AppendLine("  foreach 维度 in 维度列表:");
        builder.AppendLine("    点击维度并等待评价区域刷新");
        builder.AppendLine("    动态识别当前维度下全部评价标签");
        builder.AppendLine("    foreach 标签 in 标签列表:");
        builder.AppendLine("      根据标签颜色/样式判断优点或缺点");
        builder.AppendLine("      点击标签并等待评价列表刷新");
        builder.AppendLine("      foreach 评价卡片 in 评价列表:");
        builder.AppendLine("        展开完整评价正文");
        builder.AppendLine("        提取正文、详情链接");
        builder.AppendLine("        写入车型 Sheet");
        builder.AppendLine("保存结果 Excel");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## 5. 动态识别规则");
        builder.AppendLine("- 维度 Tab：从口碑区域内所有可点击 Tab/标签读取文本，去重后形成维度列表，页面出现哪些维度就采集哪些维度。");
        builder.AppendLine("- 评价标签：从当前维度下的标签容器动态读取，记录标签文本与样式颜色。");
        builder.AppendLine("- 优缺点判断：优先根据标签颜色、class 或视觉样式判断；无法判断时写入 `未分类`。");
        builder.AppendLine("- 评价卡片：以评价正文、用户信息、详情链接的共同父容器作为卡片边界。");
        builder.AppendLine();
        builder.AppendLine("## 6. 数据提取字段与 Excel 写入规则");
        builder.AppendLine("| 字段 | 来源 | 说明 |");
        builder.AppendLine("|---|---|---|");
        builder.AppendLine("| 序号 | 写入时生成 | 从 1 开始递增 |");
        builder.AppendLine("| 平台 | 固定值 | 懂车帝 |");
        builder.AppendLine("| 维度 | 当前维度 Tab | 页面存在什么维度就写什么维度 |");
        builder.AppendLine("| 优缺点 | 标签样式判断 | 优点/缺点/未分类 |");
        builder.AppendLine("| 评价内容 | 评价详情页或展开正文 | 尽量采集完整原文 |");
        builder.AppendLine("| 评价链接 | 详情页 URL 或卡片链接 | 无链接时留空并记录日志 |");
        builder.AppendLine();
        builder.AppendLine("## 7. 等待、异常与日志策略");
        builder.AppendLine("- 页面跳转后等待关键元素出现，不使用纯固定 sleep。维度/标签点击后等待评价列表内容发生变化。");
        builder.AppendLine("- 某车型搜索不到结果时，在主表状态列写入 `未找到车型`，继续下一个车型。");
        builder.AppendLine("- 某维度、标签或评价抓取失败时，记录失败原因，不影响其他维度和车型。");
        builder.AppendLine("- 长流程应定期保存中间结果，支持断点续跑。");
        if (!string.IsNullOrWhiteSpace(extraInstruction))
        {
            builder.AppendLine();
            builder.AppendLine("## 8. 用户补充要求");
            builder.AppendLine(extraInstruction);
        }
        return builder.ToString();
    }

    private static string GenerateGenericMarkdown(WorkflowSession session, SourceMaterialBundle materials, string? extraInstruction)
    {
        var hasSteps = session.Steps.Count > 0;
        var projectName = string.IsNullOrWhiteSpace(session.ProjectName) ? "未命名流程" : session.ProjectName;
        var builder = new StringBuilder();
        builder.AppendLine($"# {projectName}");
        builder.AppendLine();
        builder.AppendLine("## 一、流程概述");
        builder.AppendLine();
        builder.AppendLine(hasSteps
            ? $"- **目标**：通过 APA 自动化完成“{projectName}”相关业务处理，并结合录制示例、输入样例、输出样例和参考资料形成可循环执行的业务流程。"
            : $"- **目标**：通过 APA 自动化完成“{projectName}”相关资料处理，读取输入资料并按规则生成指定输出成果。");
        builder.AppendLine("- **触发方式**：手动运行；如需定时调度或被其他流程调用，待用户确认。");
        builder.AppendLine("- **最终产出**：目标业务输出文件或系统记录、每个输入对象的处理状态、执行日志和流程结果变量。");
        builder.AppendLine();
        builder.AppendLine("## 二、输入与输出");
        builder.AppendLine();
        builder.AppendLine("### 输入");
        builder.AppendLine();
        AppendInputTable(builder, materials);
        builder.AppendLine();
        builder.AppendLine("### 输出");
        builder.AppendLine();
        builder.AppendLine("| 输出项 | 格式 | 说明 |");
        builder.AppendLine("|--------|------|------|");
        builder.AppendLine("| 目标业务输出 | 文件/系统记录 | 根据输入资料和输出样例生成或更新，具体字段结构以资料中的输出样例、模板或用户补充要求为准 |");
        builder.AppendLine("| 处理状态 | JSON/日志/表格状态列 | 记录每个输入对象的处理结果，例如处理成功、数据缺失、未找到对象、校验失败或处理失败及异常原因 |");
        builder.AppendLine("| 执行日志 | 日志 | 记录读取资料、循环处理、页面等待、数据写入、异常重试和最终保存结果 |");
        builder.AppendLine("| 流程结果变量 | boolean/status | 正常完成时返回成功；关键输入缺失、保存失败或不可恢复异常时返回失败状态或抛出明确异常 |");
        builder.AppendLine();
        builder.AppendLine("## 三、操作步骤（按执行顺序编号）");
        builder.AppendLine();
        AppendStep(
            builder,
            1,
            "读取并校验输入资料",
            "本地文件系统、用户选择的需求资料、输入样例、输出样例和参考附件。",
            "读取全部资料，识别每个资料的角色，包括粗略需求、输入样例、输出样例、规则说明、模板、参考材料或输出目标。",
            "文件名、文件类型、Excel Sheet、表头字段、文档正文、PPT 页面、图片资料说明。",
            "确认关键资料可读取，得到可用于后续分析的资料清单和字段结构。",
            "关键文件不存在、文件为空、必要 Sheet 或字段缺失时，记录缺失项并中止或转人工确认。");

        AppendStep(
            builder,
            2,
            "解析业务对象和输出结构",
            "输入资料、输入样例、输出样例和用户补充要求。",
            "解析待处理对象、字段含义、记录粒度、变量来源和输出结构；如果资料中存在多行记录、多份文件、多段文档、多个分类、多个 Tab、多个标签或分页结果，应按动态集合处理。",
            "输入记录集合、业务对象字段、输出字段、文件命名规则、覆盖或追加策略。",
            "形成待处理对象集合、字段映射关系和目标输出结构。",
            "字段含义无法判断时标记为待确认；关键字段缺失时记录异常并中止。");

        if (hasSteps)
        {
            AppendStep(
                builder,
                3,
                "理解录制示例中的人工操作意图",
                "录制步骤、页面可见信息和截图视觉证据。",
                "把录制步骤作为代表性示例，提取人工操作意图、业务入口、关键元素、成功判定和截图中的可见页面结构。",
                "录制步骤标题、页面标题、操作对象、录入值、截图中的可见文本和业务控件。",
                "得到可复用的业务路径和页面操作线索，但不把示例对象、示例标签、示例行号或示例按钮写死。",
                "录制信息不足时，结合资料推断；仍无法判断时输出待人工确认项。");

            AppendStep(
                builder,
                4,
                "循环处理业务对象并写入结果",
                "业务系统、输入记录集合、动态页面集合或资料集合。",
                "按照资料和实时状态识别同类动态集合，执行 foreach 输入记录/业务对象/动态集合 的循环；每轮完成进入、条件判断、数据提取、字段校验和结果写入。",
                "输入对象、动态分类、维度、标签、表格行、详情项、分页结果、附件列表和输出字段。",
                "每个业务对象处理完成后，输出对应结果并记录来源、状态和异常原因。",
                "单个对象处理失败时记录失败原因；可跳过的异常继续处理后续对象，不可恢复异常中止。");

            AppendStep(
                builder,
                5,
                "保存成果并结束流程",
                "目标输出文件、目标系统记录和执行日志。",
                "保存目标文件或更新目标系统记录，输出处理日志和执行结果；存在业务歧义时输出待人工确认项。",
                "输出路径、文件命名规则、状态字段、日志记录和流程结果变量。",
                "目标成果已保存或系统记录已更新，日志记录完整，流程返回明确成功或失败状态。",
                "保存失败时重试；仍失败则记录错误并返回失败状态。");
        }
        else
        {
            AppendStep(
                builder,
                3,
                "按资料结构循环处理",
                "输入记录、待处理文件、文档段落、模板页或数据表。",
                "按照资料结构执行 foreach 输入记录/待处理文件/文档段落/模板页 的循环，完成数据读取、字段清洗、业务规则匹配、内容生成或格式转换。",
                "必填字段、格式规则、重复项、异常值、模板字段和输出字段。",
                "每轮处理得到可写入目标成果的数据或内容。",
                "部分资料读取失败时记录失败原因并继续处理其他可用资料；关键资料缺失时中止。");

            AppendStep(
                builder,
                4,
                "写入并保存输出成果",
                "目标文件、目标表格、目标文档或结构化数据输出。",
                "将处理结果写入目标文件、生成新文档、更新表格或输出结构化数据；写入前确认输出路径、命名规则、字段顺序和覆盖/追加策略。",
                "输出路径、目标 Sheet、字段顺序、文件命名规则、覆盖或追加开关。",
                "输出成果保存完成，执行日志记录处理数量和异常摘要。",
                "写入失败时重试；仍失败则记录错误并返回失败状态。");
        }
        builder.AppendLine();
        builder.AppendLine("## 四、业务规则与约束");
        builder.AppendLine();
        builder.AppendLine("- 规则1：录制步骤和截图只作为代表性示例，最终流程必须以输入资料、输出样例和实时页面/文件状态为准。");
        builder.AppendLine("- 规则2：页面或文件中的同类对象必须按动态集合遍历；不得只处理录制时演示的单个分类、单个标签、单行记录或单个文件。");
        builder.AppendLine("- 规则3：长流程应在每个输入对象处理完成后保存中间状态，支持失败后从已完成位置继续。");
        builder.AppendLine("- 规则4：输出路径、命名规则、覆盖/追加策略、超时时间、重试次数和必要账号/环境配置如资料未明确，均标记为待确认。");
        builder.AppendLine();
        builder.AppendLine("## 五、异常场景");
        builder.AppendLine();
        builder.AppendLine("| 异常情况 | 处理方式 |");
        builder.AppendLine("|----------|----------|");
        builder.AppendLine("| 关键输入资料缺失或为空 | 记录缺失项并中止流程，等待用户补充资料 |");
        builder.AppendLine("| 必要字段、Sheet 或输出模板缺失 | 记录异常明细；无法推断时转人工确认 |");
        builder.AppendLine("| 页面或文件加载超时 | 按配置重试；仍失败则记录错误并中止或跳过当前对象 |");
        builder.AppendLine("| 数据为空 | 记录警告；是否继续执行按业务规则或用户补充要求决定，未明确时标记待确认 |");
        builder.AppendLine("| 输出写入失败 | 重试写入；仍失败则记录错误并返回失败状态 |");
        builder.AppendLine();
        builder.AppendLine("## 六、参考信息（可选但非常有帮助）");
        builder.AppendLine();
        builder.AppendLine(hasSteps
            ? "- **截图**：录制步骤中的可用截图会作为视觉证据参与需求生成；最终执行时不依赖截图路径。"
            : "- **截图**：无录制截图；如流程依赖界面操作，建议补充关键页面截图。");
        builder.AppendLine(materials.Files.Any()
            ? $"- **示例数据**：已加入 {materials.Files.Count} 个资料文件，包含输入样例、输出样例或参考附件。"
            : "- **示例数据**：待补充输入样例和输出样例。");
        builder.AppendLine("- **已有账号/凭据**：未提供；涉及登录时仅记录账号来源和权限要求，不记录真实密码。");
        if (!string.IsNullOrWhiteSpace(extraInstruction))
        {
            builder.AppendLine();
            builder.AppendLine("- **用户补充要求**：" + extraInstruction);
        }
        return builder.ToString();
    }

    private static void AppendInputTable(StringBuilder builder, SourceMaterialBundle materials)
    {
        builder.AppendLine("| 参数名 | 类型 | 必填 | 默认值 | 说明 |");
        builder.AppendLine("|--------|------|------|--------|------|");
        var hasRows = false;
        foreach (var file in materials.Files)
        {
            var parameterName = Path.GetFileNameWithoutExtension(file.FileName)
                .Replace(" ", "_", StringComparison.Ordinal);
            builder.AppendLine($"| {parameterName} | file | 是 | \"\" | {file.FileName}，类型：{file.Kind}，{file.Status} |");
            hasRows = true;
        }

        if (!hasRows)
        {
            builder.AppendLine("| source_materials | file/table/document | 是 | \"\" | 待用户补充，可包含需求文档、输入样例、输出样例、截图说明或参考附件 |");
        }

        builder.AppendLine("| output_dir | string | 否 | 工作区导出目录 | 输出成果和日志的保存目录 |");
        builder.AppendLine("| overwrite_mode | boolean/string | 否 | 待确认 | 控制已有结果覆盖、追加或跳过的策略 |");
        builder.AppendLine("| retry_count | number | 否 | 3 | 页面、文件或写入失败时的最大重试次数 |");
    }

    private static void AppendStep(
        StringBuilder builder,
        int number,
        string title,
        string target,
        string action,
        string keyElements,
        string expectedResult,
        string failureHandling)
    {
        builder.AppendLine($"### 步骤{number}：{title}");
        builder.AppendLine($"- **操作对象**：{target}");
        builder.AppendLine($"- **具体动作**：{action}");
        builder.AppendLine($"- **关键元素**：{keyElements}");
        builder.AppendLine($"- **预期结果**：{expectedResult}");
        builder.AppendLine($"- **失败处理**：{failureHandling}");
        builder.AppendLine();
    }

    private static void AppendFileStructure(StringBuilder builder, SourceMaterialBundle materials)
    {
        foreach (var workbook in materials.Workbooks)
        {
            builder.AppendLine($"- Excel `{workbook.FileName}`");
            foreach (var sheet in workbook.Worksheets)
            {
                builder.AppendLine($"  - Sheet `{sheet.Name}`：{sheet.RowCount} 行，{sheet.ColumnCount} 列；表头：{string.Join("、", sheet.Headers)}");
            }
        }
        foreach (var document in materials.Documents)
        {
            builder.AppendLine($"- 文档 `{document.FileName}`：{document.Paragraphs.Count} 个段落。");
        }
        foreach (var presentation in materials.Presentations)
        {
            builder.AppendLine($"- PPT `{presentation.FileName}`：{presentation.Slides.Count} 页可读文本。");
        }
        foreach (var file in materials.Files.Where(file => file.Workbook is null && file.Document is null && file.Presentation is null))
        {
            builder.AppendLine($"- 其他资料 `{file.FileName}`：{file.Status}");
        }
    }

    private static string GuessModelColumn(WorksheetSummary? sheet)
    {
        if (sheet is null || sheet.Headers.Count == 0)
        {
            return "车型列";
        }

        var match = sheet.Headers.Select((header, index) => new { header, index })
            .FirstOrDefault(item => item.header.Contains("车型", StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return $"未明确；当前表头：{string.Join("、", sheet.Headers)}";
        }

        return $"{ColumnName(match.index + 1)} 列（{match.header}）";
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }
        return name;
    }

    private static string BuildSpecJson(
        WorkflowSession session,
        SourceMaterialBundle materials,
        string mode,
        IReadOnlyList<LlmImageAttachment> visualAttachments)
    {
        var spec = new
        {
            mode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            projectName = session.ProjectName,
            recordedStepCount = session.Steps.Count,
            visualEvidence = visualAttachments.Select(image => new
            {
                image.Label,
                image.MediaType,
                fileName = Path.GetFileName(image.Path)
            }),
            sourceFiles = materials.Files.Select(file => new
            {
                file.FileName,
                file.Kind,
                file.Status,
                embeddedImageCount = file.EmbeddedImages.Count
            }),
            workbooks = materials.Workbooks.Select(workbook => new
            {
                workbook.FileName,
                worksheets = workbook.Worksheets.Select(sheet => new
                {
                    sheet.Name,
                    sheet.RowCount,
                    sheet.ColumnCount,
                    sheet.Headers
                })
            }),
            documents = materials.Documents.Select(document => new
            {
                document.FileName,
                paragraphCount = document.Paragraphs.Count
            }),
            presentations = materials.Presentations.Select(presentation => new
            {
                presentation.FileName,
                slideCount = presentation.Slides.Count
            }),
            generalizedLoops = new[]
            {
                "foreach source file",
                "foreach input record",
                "foreach dynamic collection",
                "foreach output item"
            }
        };

        return JsonSerializer.Serialize(spec, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() }
        });
    }
}

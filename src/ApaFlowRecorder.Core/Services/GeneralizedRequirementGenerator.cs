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
        CancellationToken cancellationToken = default)
    {
        var prompt = _promptBuilder.Build(session, materials, extraInstruction, promptTemplate);
        var visualAttachments = BuildVisualAttachments(session, materials);
        if (settings.IsConfigured)
        {
            var markdown = await _chatClient.CompleteAsync(settings, prompt, visualAttachments, cancellationToken);
            var validation = RequirementDocumentValidator.Validate(markdown);
            var generationMode = $"LLM: {settings.ProviderName}/{settings.Model}";
            if (!validation.IsValid)
            {
                var repairPrompt = RequirementDocumentValidator.BuildRepairPrompt(markdown, validation);
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
        builder.AppendLine("# 项目需求描述");
        builder.AppendLine();
        builder.AppendLine("## 项目目标");
        builder.AppendLine(hasSteps
            ? $"通过 APA 自动化完成“{projectName}”相关业务处理。流程需要结合输入样例、输出样例、参考资料和录制的代表性示例，识别待处理业务对象、页面路径、动态集合、字段规则和输出结构，形成可循环执行、可校验、可恢复的自动化闭环。"
            : $"通过 APA 自动化完成“{projectName}”相关资料处理。本流程属于纯资料处理场景，可以不包含界面操作，重点在于读取输入资料、解析字段结构、按规则处理数据或文档，并生成指定输出成果。");
        builder.AppendLine(hasSteps
            ? "本流程目标不是简单复刻录制点击。不要把录制步骤当成固定点击脚本，而是将少量演示动作理解为人工业务意图：当示例落在列表、Tab、分类、标签、表格行、分页结果或文件集合中时，应抽象为动态集合遍历，并结合资料推断完整处理范围。"
            : "本流程目标不是生成泛泛的说明，而是把粗略需求、输入样例、输出样例和参考附件整理为 APA 可直接理解的执行需求。");
        builder.AppendLine();
        builder.AppendLine("## 流程步骤");
        builder.AppendLine("详细描述每个步骤的具体操作和验证方式：");
        builder.AppendLine();
        builder.AppendLine("第一步：读取并校验全部输入资料。识别每个资料的角色，包括粗略需求、输入样例、输出样例、规则说明、模板、参考材料或输出目标。若关键文件不存在、文件为空、必要 Sheet 或字段缺失，则抛出输入资料异常，并记录缺失项。");
        builder.AppendLine();
        builder.AppendLine("第二步：解析输入样例和参考资料，识别待处理对象、字段含义、记录粒度、变量来源和输出结构。若资料中存在多行记录、多份文件、多段文档、多个分类、多个 Tab、多个标签或分页结果，应按动态集合处理，而不是只处理示例中的单个对象。");
        builder.AppendLine();
        if (hasSteps)
        {
            builder.AppendLine("第三步：解析录制步骤中的代表性示例，提取人工操作意图、页面路径、关键元素、成功判定和截图中的可见页面结构。录制只用于说明“人想完成什么”和“页面大致如何到达”，不能把示例对象、示例标签、示例行号或示例按钮写死。");
            builder.AppendLine();
            builder.AppendLine("第四步：按照资料和页面实时状态识别同类动态集合，并执行 foreach 输入记录/业务对象/动态集合 的循环。每一轮应完成必要的页面进入、条件判断、数据提取、内容生成、字段校验和结果写入。");
            builder.AppendLine();
            builder.AppendLine("第五步：对当前业务对象内部的分类、维度、标签、表格行、详情项、分页结果或附件列表继续动态遍历。每个子项处理完成后，应写入对应输出字段，并记录来源、状态和异常原因。");
            builder.AppendLine();
            builder.AppendLine("第六步：完成全部输入对象处理后，保存目标文件或更新目标系统记录，输出处理日志和执行结果。若存在无法自动判断的业务歧义，应输出待人工确认项，而不是默默使用示例值。");
        }
        else
        {
            builder.AppendLine("第三步：按照资料结构执行 foreach 输入记录/待处理文件/文档段落/模板页 的循环。每一轮应完成数据读取、字段清洗、业务规则匹配、内容生成或格式转换，并校验必填字段、格式、重复项和异常值。");
            builder.AppendLine();
            builder.AppendLine("第四步：将处理结果写入目标文件、生成新文档、更新表格或输出结构化数据。写入前应确认输出路径、命名规则、字段顺序和覆盖/追加策略。");
            builder.AppendLine();
            builder.AppendLine("第五步：保存输出成果并记录处理日志。若部分资料读取失败，应记录失败原因并继续处理其他可用资料；若关键资料缺失导致流程无法继续，则返回明确失败状态。");
        }
        builder.AppendLine();
        builder.AppendLine("## 流程输入");
        AppendFlowInputs(builder, materials);
        builder.AppendLine();
        builder.AppendLine("## 流程输出");
        builder.AppendLine("1. 目标业务输出文件或系统记录：由 APA 根据输入资料和输出样例生成或更新，具体格式以资料中的输出样例、模板或用户补充要求为准。");
        builder.AppendLine("2. 处理状态：记录每个输入对象的处理结果，例如处理成功、数据缺失、未找到对象、校验失败或处理失败及异常原因。");
        builder.AppendLine("3. 执行日志：记录读取资料、循环处理、页面等待、数据写入、异常重试和最终保存结果。");
        builder.AppendLine("4. 流程结果变量：流程正常完成时返回 true；关键输入缺失、保存失败或不可恢复异常时返回 false 或抛出明确异常。");
        builder.AppendLine();
        builder.AppendLine("## 约束与异常处理");
        builder.AppendLine("1. 录制步骤和截图只作为代表性示例，最终流程必须以输入资料、输出样例和实时页面/文件状态为准。");
        builder.AppendLine("2. 页面或文件中的同类对象必须按动态集合遍历；不得只处理录制时演示的单个分类、单个标签、单行记录或单个文件。");
        builder.AppendLine("3. 对页面加载、文件读取、字段缺失、格式错误、无数据、重复数据、写入失败和访问限流分别记录异常类型和摘要。");
        builder.AppendLine("4. 长流程应在每个输入对象处理完成后保存中间状态，支持失败后从已完成位置继续。");
        if (!string.IsNullOrWhiteSpace(extraInstruction))
        {
            builder.AppendLine();
            builder.AppendLine("## 用户补充要求");
            builder.AppendLine(extraInstruction);
        }
        return builder.ToString();
    }

    private static void AppendFlowInputs(StringBuilder builder, SourceMaterialBundle materials)
    {
        var index = 1;
        foreach (var file in materials.Files)
        {
            builder.AppendLine($"{index}. {file.FileName}：{file.Kind}，{file.Status}。");
            index++;
        }

        if (!materials.Files.Any())
        {
            builder.AppendLine("1. 输入资料：待用户补充，可包含需求文档、输入样例、输出样例、截图说明或参考附件。");
            index = 2;
        }

        builder.AppendLine($"{index}. 输入样例：用于识别待处理对象、字段含义、记录粒度、变量来源和动态集合范围。");
        builder.AppendLine($"{index + 1}. 输出样例：用于确定目标字段、结果结构、写入位置、文件命名规则和验收标准。");
        builder.AppendLine($"{index + 2}. 执行参数：包括输出目录、覆盖/追加策略、超时时间、重试次数、断点续跑开关和必要的账号/环境配置。");
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

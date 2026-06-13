using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ApaFlowRecorder.Core.Tests;

public class GeneralizedRequirementGeneratorTests
{
    [Fact]
    public async Task Rule_based_output_treats_recording_as_representative_human_intent_not_fixed_clicks()
    {
        var session = new WorkflowSession { ProjectName = "业务资料采集" };
        session.Steps.Add(new RecordedStep
        {
            Action = RecordedAction.Click,
            Title = "点击某个分类标签查看详情",
            Element = new ElementSnapshot { Text = "分类A", Role = "button" }
        });

        var materials = BuildRepresentativeMaterials();

        var result = await new GeneralizedRequirementGenerator().GenerateAsync(session, materials, new LlmSettings(), null);

        Assert.Equal("Rule-based fallback", result.GenerationMode);
        Assert.StartsWith("# 业务资料采集", result.Markdown);
        Assert.Contains("## 一、流程概述", result.Markdown);
        Assert.Contains("## 二、输入与输出", result.Markdown);
        Assert.Contains("| 参数名 | 类型 | 必填 | 默认值 | 说明 |", result.Markdown);
        Assert.Contains("| 输出项 | 格式 | 说明 |", result.Markdown);
        Assert.Contains("## 三、操作步骤（按执行顺序编号）", result.Markdown);
        Assert.Contains("### 步骤1：", result.Markdown);
        Assert.Contains("- **操作对象**：", result.Markdown);
        Assert.Contains("- **具体动作**：", result.Markdown);
        Assert.Contains("- **关键元素**：", result.Markdown);
        Assert.Contains("- **预期结果**：", result.Markdown);
        Assert.Contains("- **失败处理**：", result.Markdown);
        Assert.Contains("## 四、业务规则与约束", result.Markdown);
        Assert.Contains("## 五、异常场景", result.Markdown);
        Assert.Contains("## 六、参考信息（可选但非常有帮助）", result.Markdown);
        Assert.Contains("代表性示例", result.Markdown);
        Assert.Contains("录制步骤和截图只作为代表性示例", result.Markdown);
        Assert.Contains("动态集合", result.Markdown);
        Assert.Contains("输入样例", result.Markdown);
        Assert.Contains("输出样例", result.Markdown);
        Assert.DoesNotContain("录制示例如何参与泛化", result.Markdown);
        Assert.DoesNotContain("需求推断说明", result.Markdown);
        Assert.DoesNotContain("流程图", result.Markdown);
        Assert.DoesNotContain("鎳", result.Markdown);
        Assert.DoesNotContain("�", result.Markdown);
    }

    [Fact]
    public async Task Generic_rule_based_output_supports_file_only_workflows_without_recorded_steps()
    {
        var session = new WorkflowSession { ProjectName = "月度报表整理" };
        var materials = new SourceMaterialBundle
        {
            Files =
            [
                new SourceMaterialFile
                {
                    FileName = "销售明细.xlsx",
                    Kind = "xlsx",
                    Workbook = new WorkbookSummary
                    {
                        FileName = "销售明细.xlsx",
                        Worksheets =
                        [
                            new WorksheetSummary
                            {
                                Name = "明细",
                                Headers = ["日期", "区域", "金额"],
                                RowCount = 100,
                                ColumnCount = 3
                            }
                        ]
                    }
                },
                new SourceMaterialFile
                {
                    FileName = "处理规则.docx",
                    Kind = "docx",
                    Document = new DocumentSummary
                    {
                        FileName = "处理规则.docx",
                        Text = "按区域汇总并生成月度报告。",
                        Paragraphs = ["按区域汇总并生成月度报告。"]
                    }
                }
            ]
        };

        var result = await new GeneralizedRequirementGenerator().GenerateAsync(session, materials, new LlmSettings(), "无需界面操作");

        Assert.Contains("资料处理", result.Markdown);
        Assert.Contains("销售明细.xlsx", result.Markdown);
        Assert.Contains("处理规则.docx", result.SpecJson);
        Assert.Contains("foreach 输入记录", result.Markdown);
        Assert.Contains("## 二、输入与输出", result.Markdown);
        Assert.Contains("### 输入", result.Markdown);
        Assert.Contains("### 输出", result.Markdown);
    }

    [Fact]
    public void Prompt_builder_requires_clean_requirement_document_template_not_analysis_report()
    {
        var prompt = new GeneralizedRequirementPromptBuilder().Build(
            new WorkflowSession { ProjectName = "批量资料处理" },
            BuildRepresentativeMaterials(),
            null);

        Assert.Contains("必须严格填充下面的“需求文档模板”", prompt);
        Assert.Contains("# [流程名称]", prompt);
        Assert.Contains("## 一、流程概述", prompt);
        Assert.Contains("## 二、输入与输出", prompt);
        Assert.Contains("### 输入", prompt);
        Assert.Contains("### 输出", prompt);
        Assert.Contains("## 三、操作步骤（按执行顺序编号）", prompt);
        Assert.Contains("### 步骤1：[步骤标题]", prompt);
        Assert.Contains("## 五、异常场景", prompt);
        Assert.Contains("不要输出“录制示例如何参与泛化”", prompt);
        Assert.Contains("不要输出 Mermaid、ASCII 流程图、伪代码大段代码块", prompt);
    }

    [Fact]
    public void Prompt_builder_includes_materials_recorded_steps_and_intent_inference_rules()
    {
        var screenshotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(screenshotPath, [1, 2, 3]);
        var session = new WorkflowSession();
        try
        {
            session.Steps.Add(new RecordedStep
            {
                Action = RecordedAction.Click,
                Title = "点击查看示例分类",
                PageUrl = "https://example.test/workflow/sample",
                ScreenshotPath = screenshotPath
            });
            var materials = BuildRepresentativeMaterials();
            materials.Files.Add(new SourceMaterialFile
            {
                FileName = "说明.pptx",
                Kind = "pptx",
                Presentation = new PresentationSummary
                {
                    FileName = "说明.pptx",
                    Slides = [new SlideSummary { Number = 1, Text = "采集页面分类和详情正文" }]
                }
            });

            var prompt = new GeneralizedRequirementPromptBuilder().Build(session, materials, "不要写死标签");

            Assert.Contains("点击查看示例分类", prompt);
            Assert.Contains("输入样例.xlsx", prompt);
            Assert.Contains("说明.pptx", prompt);
            Assert.Contains("采集页面分类", prompt);
            Assert.Contains("不要写死标签", prompt);
            Assert.Contains("代表性示例", prompt);
            Assert.Contains("推断完整业务意图", prompt);
            Assert.Contains("输入样例", prompt);
            Assert.Contains("输出样例", prompt);
            Assert.Contains("遍历全部同类对象", prompt);
            Assert.Contains("截图=已附带", prompt);
            Assert.DoesNotContain("鎳", prompt);
            Assert.DoesNotContain("�", prompt);
        }
        finally
        {
            File.Delete(screenshotPath);
        }
    }

    [Fact]
    public async Task Llm_generation_sends_material_image_files_as_visual_evidence()
    {
        var screenshotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(screenshotPath, [1, 2, 3]);
        var handler = new CapturingHandler();
        var generator = new GeneralizedRequirementGenerator(new OpenAiCompatibleChatClient(new HttpClient(handler)));
        var materials = new SourceMaterialBundle
        {
            Files =
            [
                new SourceMaterialFile
                {
                    Path = screenshotPath,
                    FileName = "页面截图.png",
                    Kind = "png",
                    Status = "已加入；图片将作为视觉证据"
                }
            ]
        };

        try
        {
            await generator.GenerateAsync(
                new WorkflowSession { ProjectName = "图像资料流程" },
                materials,
                new LlmSettings
                {
                    ProviderName = "Test",
                    BaseUrl = "https://api.example.test/v1",
                    Model = "vision-model",
                    ApiKey = "plain-text-key",
                    Temperature = 0.1
                },
                null);

            using var requestJson = JsonDocument.Parse(handler.RequestBody);
            var userContent = requestJson.RootElement
                .GetProperty("messages")[1]
                .GetProperty("content");

            Assert.Equal(JsonValueKind.Array, userContent.ValueKind);
            Assert.Contains(userContent.EnumerateArray(), part =>
                part.GetProperty("type").GetString() == "text" &&
                part.GetProperty("text").GetString()!.Contains("资料图片：页面截图.png", StringComparison.Ordinal));
            Assert.Contains(userContent.EnumerateArray(), part =>
                part.GetProperty("type").GetString() == "image_url" &&
                part.GetProperty("image_url").GetProperty("url").GetString() == "data:image/png;base64,AQID");
        }
        finally
        {
            File.Delete(screenshotPath);
        }
    }

    [Fact]
    public async Task Llm_generation_sends_office_embedded_images_as_visual_evidence()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(imagePath, [7, 8, 9]);
        var handler = new CapturingHandler();
        var generator = new GeneralizedRequirementGenerator(new OpenAiCompatibleChatClient(new HttpClient(handler)));
        var materials = new SourceMaterialBundle
        {
            Files =
            [
                new SourceMaterialFile
                {
                    Path = @"D:\docs\需求说明.docx",
                    FileName = "需求说明.docx",
                    Kind = "docx",
                    EmbeddedImages =
                    [
                        new SourceMaterialImage
                        {
                            Path = imagePath,
                            Label = "Word 内嵌图片：需求说明.docx / image1.png",
                            MediaType = "image/png"
                        }
                    ]
                }
            ]
        };

        try
        {
            await generator.GenerateAsync(
                new WorkflowSession { ProjectName = "图文需求流程" },
                materials,
                new LlmSettings
                {
                    ProviderName = "Test",
                    BaseUrl = "https://api.example.test/v1",
                    Model = "vision-model",
                    ApiKey = "plain-text-key",
                    Temperature = 0.1
                },
                null);

            using var requestJson = JsonDocument.Parse(handler.RequestBody);
            var userContent = requestJson.RootElement
                .GetProperty("messages")[1]
                .GetProperty("content");

            Assert.Equal(JsonValueKind.Array, userContent.ValueKind);
            Assert.Contains(userContent.EnumerateArray(), part =>
                part.GetProperty("type").GetString() == "text" &&
                part.GetProperty("text").GetString()!.Contains("Word 内嵌图片", StringComparison.Ordinal));
            Assert.Contains(userContent.EnumerateArray(), part =>
                part.GetProperty("type").GetString() == "image_url" &&
                part.GetProperty("image_url").GetProperty("url").GetString() == "data:image/png;base64,BwgJ");
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Llm_generation_repairs_markdown_when_model_ignores_required_template()
    {
        var handler = new RepairingHandler();
        var generator = new GeneralizedRequirementGenerator(new OpenAiCompatibleChatClient(new HttpClient(handler)));

        var result = await generator.GenerateAsync(
            new WorkflowSession { ProjectName = "模板修正流程" },
            BuildRepresentativeMaterials(),
            new LlmSettings
            {
                ProviderName = "Test",
                BaseUrl = "https://api.example.test/v1",
                Model = "chat-model",
                ApiKey = "plain-text-key",
                Temperature = 0.1
            },
            null);

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("template repair", result.GenerationMode);
        Assert.StartsWith("# 模板修正流程", result.Markdown);
        Assert.Contains("## 五、异常场景", result.Markdown);
        using var repairRequestJson = JsonDocument.Parse(handler.LastRequestBody);
        var repairPrompt = repairRequestJson.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString();
        Assert.Contains("下面是一份不合格的 APA 需求文档", repairPrompt);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"# 图像资料流程\\n\\n## 一、流程概述\\n\\n- **目标**：生成需求。\\n- **触发方式**：手动运行。\\n- **最终产出**：输出文档。\\n\\n## 二、输入与输出\\n\\n### 输入\\n\\n| 参数名 | 类型 | 必填 | 默认值 | 说明 |\\n|--------|------|------|--------|------|\\n| input | file | 是 | \\\"\\\" | 输入资料 |\\n\\n### 输出\\n\\n| 输出项 | 格式 | 说明 |\\n|--------|------|------|\\n| 文档 | Markdown | 需求文档 |\\n\\n## 三、操作步骤（按执行顺序编号）\\n\\n### 步骤1：处理资料\\n- **操作对象**：输入资料。\\n- **具体动作**：读取资料并生成文档。\\n- **关键元素**：资料正文。\\n- **预期结果**：得到需求文档。\\n- **失败处理**：失败时记录日志。\\n\\n## 四、业务规则与约束\\n\\n- 规则1：待确认。\\n\\n## 五、异常场景\\n\\n| 异常情况 | 处理方式 |\\n|----------|----------|\\n| 失败 | 记录日志 |\\n\\n## 六、参考信息（可选但非常有帮助）\\n\\n- **截图**：待补充。\\n- **示例数据**：待补充。\\n- **已有账号/凭据**：无。\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class RepairingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var content = RequestCount == 1
                ? "# 需求分析报告\n\n## 录制示例如何参与泛化\n分析内容。"
                : "# 模板修正流程\n\n## 一、流程概述\n\n- **目标**：目标。\n- **触发方式**：手动运行。\n- **最终产出**：输出。\n\n## 二、输入与输出\n\n### 输入\n\n| 参数名 | 类型 | 必填 | 默认值 | 说明 |\n|--------|------|------|--------|------|\n| input | file | 是 | \"\" | 输入资料 |\n\n### 输出\n\n| 输出项 | 格式 | 说明 |\n|--------|------|------|\n| output | Markdown | 输出文档 |\n\n## 三、操作步骤（按执行顺序编号）\n\n### 步骤1：处理\n- **操作对象**：输入资料。\n- **具体动作**：处理。\n- **关键元素**：资料正文。\n- **预期结果**：输出。\n- **失败处理**：异常记录。\n\n## 四、业务规则与约束\n\n- 规则1：待确认。\n\n## 五、异常场景\n\n| 异常情况 | 处理方式 |\n|----------|----------|\n| 异常 | 记录 |\n\n## 六、参考信息（可选但非常有帮助）\n\n- **截图**：无。\n- **示例数据**：待补充。\n- **已有账号/凭据**：无。";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        choices = new[]
                        {
                            new
                            {
                                message = new
                                {
                                    content
                                }
                            }
                        }
                    }),
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private static SourceMaterialBundle BuildRepresentativeMaterials()
    {
        return new SourceMaterialBundle
        {
            Files =
            [
                new SourceMaterialFile
                {
                    FileName = "输入样例.xlsx",
                    Kind = "xlsx",
                    Workbook = new WorkbookSummary
                    {
                        FileName = "输入样例.xlsx",
                        Worksheets =
                        [
                            new WorksheetSummary
                            {
                                Name = "输入样例",
                                Headers = ["序号", "业务对象", "处理范围"],
                                RowCount = 3,
                                ColumnCount = 7,
                                PreviewRows =
                                [
                                    ["序号", "业务对象", "处理范围"],
                                    ["1", "示例对象", "全部分类"]
                                ]
                            }
                        ]
                    }
                },
                new SourceMaterialFile
                {
                    FileName = "粗略需求.docx",
                    Kind = "docx",
                    Document = new DocumentSummary
                    {
                        FileName = "粗略需求.docx",
                        Text = "根据输入样例逐条处理业务对象，页面中类似分类只演示一个，但实际需要遍历全部分类，最后生成输出样例要求的结构化结果。",
                        Paragraphs = ["根据输入样例逐条处理业务对象，页面中类似分类只演示一个，但实际需要遍历全部分类，最后生成输出样例要求的结构化结果。"]
                    }
                },
                new SourceMaterialFile
                {
                    FileName = "输出样例.csv",
                    Kind = "csv",
                    TextPreview = "业务对象,分类,结果,来源链接\n示例对象,分类A,示例结果,https://example.test/detail"
                },
                new SourceMaterialFile
                {
                    FileName = "参考附件.bin",
                    Kind = "bin",
                    Status = "已加入；当前版本仅读取文件名和大小",
                    TextPreview = "参考附件.bin; 128 bytes"
                }
            ]
        };
    }
}

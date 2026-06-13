using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public sealed class GeneralizedRequirementPromptBuilderTemplateTests
{
    [Fact]
    public void Build_renders_custom_template_placeholders()
    {
        var session = new WorkflowSession { ProjectName = "模板测试" };
        session.Steps.Add(new RecordedStep { Action = RecordedAction.Click, Title = "点击示例按钮" });
        var materials = new SourceMaterialBundle
        {
            Files =
            [
                new SourceMaterialFile
                {
                    FileName = "输入样例.xlsx",
                    Kind = "xlsx",
                    Status = "已读取"
                }
            ]
        };
        var template = """
        自定义角色。
        {{recorded_example}}
        {{source_materials}}
        {{extra_instruction}}
        # [流程名称]
        """;

        var prompt = new GeneralizedRequirementPromptBuilder().Build(session, materials, "补充规则", template);

        Assert.Contains("自定义角色", prompt);
        Assert.Contains("点击示例按钮", prompt);
        Assert.Contains("输入样例.xlsx", prompt);
        Assert.Contains("补充规则", prompt);
        Assert.DoesNotContain("{{recorded_example}}", prompt);
    }

    [Fact]
    public void Build_appends_required_context_when_custom_template_omits_placeholders()
    {
        var session = new WorkflowSession();
        session.Steps.Add(new RecordedStep { Action = RecordedAction.Click, Title = "代表性操作" });

        var prompt = new GeneralizedRequirementPromptBuilder().Build(session, new SourceMaterialBundle(), "补充要求", "只写固定开头");

        Assert.Contains("只写固定开头", prompt);
        Assert.Contains("# 当前录制示例", prompt);
        Assert.Contains("代表性操作", prompt);
        Assert.Contains("# 输入资料清单", prompt);
        Assert.Contains("# 用户补充要求", prompt);
    }

    [Fact]
    public void Build_injects_configurable_requirement_document_template_separately_from_prompt_template()
    {
        var documentTemplate = """
        # [流程名称]

        ## 自定义最终文档章节

        - 必须按这个模板填充。
        """;

        var prompt = new GeneralizedRequirementPromptBuilder().Build(
            new WorkflowSession { ProjectName = "模板测试" },
            new SourceMaterialBundle(),
            null,
            "只写分析规则，不包含最终模板占位符。",
            documentTemplate);

        Assert.Contains("# 最终需求文档模板", prompt);
        Assert.Contains("## 自定义最终文档章节", prompt);
        Assert.Contains("必须按这个模板填充", prompt);
    }

    [Fact]
    public void Build_recorded_example_omits_capture_technical_details()
    {
        var session = new WorkflowSession();
        session.Steps.Add(new RecordedStep
        {
            Action = RecordedAction.Click,
            Title = "查询证券余额",
            PageTitle = "证券余额查询",
            PageUrl = "https://example.test/internal/query?id=123",
            Element = new ElementSnapshot
            {
                Text = "查询",
                CssSelector = "#app > div:nth-child(2) button.primary"
            }
        });

        var prompt = new GeneralizedRequirementPromptBuilder().Build(session, new SourceMaterialBundle(), null);

        Assert.Contains("查询证券余额", prompt);
        Assert.Contains("证券余额查询", prompt);
        Assert.DoesNotContain("https://example.test/internal/query?id=123", prompt);
        Assert.DoesNotContain("#app > div:nth-child(2) button.primary", prompt);
    }
}

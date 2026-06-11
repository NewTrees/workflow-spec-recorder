using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class MarkdownExporterTests
{
    [Fact]
    public void Workflow_defaults_are_readable_chinese()
    {
        var session = new WorkflowSession();

        Assert.Equal("未命名流程", session.ProjectName);
        Assert.Contains("业务目标", session.Objective);
        Assert.Contains("Chrome 浏览器", session.Preconditions);
        Assert.DoesNotContain("鎵", session.ProjectName + session.Objective + session.Preconditions);
        Assert.DoesNotContain("璇", session.ProjectName + session.Objective + session.Preconditions);
    }

    [Fact]
    public void Renders_required_sections_variables_and_element_catalog_in_readable_chinese()
    {
        var session = new WorkflowSession
        {
            ProjectName = "报销流程",
            Objective = "自动完成报销提交流程",
            Preconditions = "使用 Chrome 浏览器，测试账号可正常登录。"
        };

        session.Steps.Add(new RecordedStep
        {
            Title = "填写账号",
            Action = RecordedAction.Fill,
            Element = new ElementSnapshot
            {
                Label = "账号",
                Role = "textbox",
                CssSelector = "#username"
            },
            VariableName = "login_account",
            SuccessCriteria = "账号输入框已填写"
        });

        session.Steps.Add(new RecordedStep
        {
            Title = "点击登录",
            Action = RecordedAction.Click,
            Element = new ElementSnapshot
            {
                Text = "登录",
                Role = "button",
                CssSelector = "button[type='submit']"
            },
            SuccessCriteria = "页面进入工作台"
        });

        var markdown = new MarkdownExporter().Export(session);

        Assert.Contains("# 报销流程 APA 需求文档", markdown);
        Assert.Contains("## 项目目标", markdown);
        Assert.Contains("## 适用范围与前置条件", markdown);
        Assert.Contains("## 详细步骤", markdown);
        Assert.Contains("## 变量定义", markdown);
        Assert.Contains("## 元素清单", markdown);
        Assert.Contains("点击登录", markdown);
        Assert.Contains("账号输入框已填写", markdown);
        Assert.Contains("`login_account`", markdown);
        Assert.Contains("#username", markdown);
        Assert.Contains("button[type='submit']", markdown);
        Assert.DoesNotContain("鎵", markdown);
        Assert.DoesNotContain("璇", markdown);
        Assert.DoesNotContain("�", markdown);
    }
}

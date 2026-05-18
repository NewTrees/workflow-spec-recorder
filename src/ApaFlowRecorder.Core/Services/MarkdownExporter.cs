using System.Text;
using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class MarkdownExporter
{
    public string Export(WorkflowSession session)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"# {session.ProjectName} APA 需求文档");
        builder.AppendLine();
        builder.AppendLine("## 项目目标");
        builder.AppendLine(session.Objective);
        builder.AppendLine();
        builder.AppendLine("## 适用范围与前置条件");
        builder.AppendLine(session.Preconditions);
        builder.AppendLine();
        builder.AppendLine("## 流程概览");
        builder.AppendLine(session.Steps.Count == 0
            ? "当前尚未录制步骤。"
            : string.Join(" → ", session.Steps.Select((step, index) => $"{index + 1}. {step.Title}")));
        builder.AppendLine();
        builder.AppendLine("## 详细步骤");

        if (session.Steps.Count == 0)
        {
            builder.AppendLine("暂无步骤。");
        }
        else
        {
            for (var index = 0; index < session.Steps.Count; index++)
            {
                var step = session.Steps[index];
                builder.AppendLine($"### 第 {index + 1} 步：{step.Title}");
                builder.AppendLine($"- 动作类型：{RenderAction(step.Action)}");
                builder.AppendLine($"- 目标元素：{step.Element?.DisplayName ?? "无"}");
                builder.AppendLine($"- 页面：{FirstNonBlank(step.PageTitle, step.PageUrl) ?? "未记录"}");
                builder.AppendLine($"- 输入值：{RenderValue(step)}");
                builder.AppendLine($"- 成功判定：{step.SuccessCriteria}");
                if (!string.IsNullOrWhiteSpace(step.Notes))
                {
                    builder.AppendLine($"- 备注：{step.Notes}");
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine("## 变量定义");
        builder.AppendLine("| 变量名 | 类型 | 描述 | 默认值 |");
        builder.AppendLine("|---|---|---|---|");
        var variables = session.BuildVariables();
        if (variables.Count == 0)
        {
            builder.AppendLine("| - | - | 当前流程未定义变量 | - |");
        }
        else
        {
            foreach (var variable in variables)
            {
                builder.AppendLine($"| `{variable.Name}` | {variable.Type} | {variable.Description} | {variable.DefaultValue ?? "-"} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 元素清单");
        builder.AppendLine("| 元素名称 | 控件类型 | 推荐定位方式 | 备选定位方式 |");
        builder.AppendLine("|---|---|---|---|");
        var elements = session.Steps
            .Where(step => step.Element is not null)
            .Select(step => step.Element!)
            .GroupBy(element => $"{element.DisplayName}|{element.Role}|{element.CssSelector}")
            .Select(group => group.First())
            .ToList();

        if (elements.Count == 0)
        {
            builder.AppendLine("| - | - | - | - |");
        }
        else
        {
            foreach (var element in elements)
            {
                var alternates = element.AlternateSelectors.Count == 0
                    ? "-"
                    : string.Join("<br>", element.AlternateSelectors);
                builder.AppendLine($"| {element.DisplayName} | {element.Role ?? element.TagName ?? "-"} | {element.CssSelector ?? "-"} | {alternates} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 等待与异常策略");
        builder.AppendLine("- 页面跳转后，等待目标 URL 或关键元素出现后再执行下一步。");
        builder.AppendLine("- 元素定位失败、页面未加载、上传文件缺失时，应抛出明确异常并终止流程。");
        builder.AppendLine("- 对登录、提交等关键节点，优先使用显式成功判定，而不是固定睡眠。");
        builder.AppendLine();
        builder.AppendLine("## 特殊说明");
        builder.AppendLine("- 请在导入 APA 前复核变量名、敏感字段和成功判定。");
        builder.AppendLine("- 若页面存在动态元素或异步刷新，建议补充更具体的等待条件。");

        return builder.ToString();
    }

    private static string RenderAction(RecordedAction action) => action switch
    {
        RecordedAction.Navigate => "打开页面",
        RecordedAction.Click => "点击",
        RecordedAction.Fill => "输入",
        RecordedAction.Select => "选择",
        RecordedAction.Upload => "上传",
        RecordedAction.Wait => "等待/校验",
        _ => action.ToString()
    };

    private static string RenderValue(RecordedStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.VariableName))
        {
            return $"变量 `{step.VariableName}`";
        }

        if (!string.IsNullOrWhiteSpace(step.LiteralValue))
        {
            return step.IsSensitive ? "敏感值" : $"`{step.LiteralValue}`";
        }

        return "-";
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

namespace ApaFlowRecorder.Core.Models;

public sealed class WorkflowSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ProjectName { get; set; } = "未命名流程";
    public string Objective { get; set; } = "请补充该流程的业务目标。";
    public string Preconditions { get; set; } = "使用 Chrome 浏览器，并确保目标系统可访问。";
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public List<RecordedStep> Steps { get; } = [];

    public IReadOnlyList<WorkflowVariable> BuildVariables()
    {
        return Steps
            .Where(step => !string.IsNullOrWhiteSpace(step.VariableName))
            .GroupBy(step => step.VariableName!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sample = group.First();
                return new WorkflowVariable
                {
                    Name = group.Key,
                    Type = sample.IsSensitive ? "credential" : sample.Action == RecordedAction.Upload ? "string" : "string",
                    Description = sample.Action switch
                    {
                        RecordedAction.Upload => $"{sample.Element?.DisplayName ?? "文件"}路径",
                        _ => $"{sample.Element?.DisplayName ?? "字段"}输入值"
                    },
                    DefaultValue = sample.IsSensitive ? "-" : null
                };
            })
            .OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}


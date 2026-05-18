namespace ApaFlowRecorder.Core.Models;

public sealed class WorkflowVariable
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? DefaultValue { get; init; }
}


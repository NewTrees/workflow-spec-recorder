namespace ApaFlowRecorder.Core.Models;

public sealed class GeneralizedRequirementResult
{
    public required string Markdown { get; init; }
    public required string SpecJson { get; init; }
    public required string GenerationMode { get; init; }
    public string Prompt { get; init; } = string.Empty;
}

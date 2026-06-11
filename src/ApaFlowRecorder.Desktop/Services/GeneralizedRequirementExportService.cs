using System.IO;
using System.Linq;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Desktop.Services;

public sealed class GeneralizedRequirementExportService
{
    private readonly OfficeMaterialReader _materialReader = new();
    private readonly GeneralizedRequirementGenerator _generator = new();

    public async Task<(string ExportDirectory, GeneralizedRequirementResult Result)> GenerateAndExportAsync(
        WorkflowSession session,
        string? workbookPath,
        string? documentPath,
        LlmSettings llmSettings,
        string? extraInstruction,
        string? promptTemplate,
        CancellationToken cancellationToken = default)
    {
        var materials = new SourceMaterialBundle
        {
            WorkbookPath = workbookPath,
            DocumentPath = documentPath
        };

        if (!string.IsNullOrWhiteSpace(workbookPath) && File.Exists(workbookPath))
        {
            materials.Workbook = _materialReader.ReadWorkbook(workbookPath);
        }

        if (!string.IsNullOrWhiteSpace(documentPath) && File.Exists(documentPath))
        {
            materials.Document = _materialReader.ReadDocument(documentPath);
        }

        var result = await _generator.GenerateAsync(session, materials, llmSettings, extraInstruction, promptTemplate, cancellationToken);
        var exportDirectory = await ExportAsync(session, result, cancellationToken);
        return (exportDirectory, result);
    }

    public async Task<(string ExportDirectory, GeneralizedRequirementResult Result)> GenerateAndExportAsync(
        WorkflowSession session,
        IEnumerable<string> materialPaths,
        LlmSettings llmSettings,
        string? extraInstruction,
        string? promptTemplate,
        CancellationToken cancellationToken = default)
    {
        var materials = new SourceMaterialBundle();
        foreach (var path in materialPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                materials.Files.Add(new SourceMaterialFile
                {
                    Path = path,
                    FileName = Path.GetFileName(path),
                    Kind = Path.GetExtension(path).TrimStart('.'),
                    Status = "文件不存在"
                });
                continue;
            }

            materials.Files.Add(_materialReader.ReadMaterial(path));
        }

        var result = await _generator.GenerateAsync(session, materials, llmSettings, extraInstruction, promptTemplate, cancellationToken);
        var exportDirectory = await ExportAsync(session, result, cancellationToken);
        return (exportDirectory, result);
    }

    private static async Task<string> ExportAsync(WorkflowSession session, GeneralizedRequirementResult result, CancellationToken cancellationToken)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var exportRoot = Path.Combine(documents, "ApaFlowRecorder", "GeneralizedExports");
        Directory.CreateDirectory(exportRoot);

        var folderName = $"{SanitizeFileName(session.ProjectName)}-generalized-{DateTime.Now:yyyyMMdd-HHmmss}";
        var exportDirectory = Path.Combine(exportRoot, folderName);
        Directory.CreateDirectory(exportDirectory);

        await File.WriteAllTextAsync(Path.Combine(exportDirectory, "APA-generalized-requirements.md"), result.Markdown, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(exportDirectory, "workflow-spec.json"), result.SpecJson, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(exportDirectory, "llm-prompt.txt"), result.Prompt, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(exportDirectory, "generation-mode.txt"), result.GenerationMode, cancellationToken);

        return exportDirectory;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "untitled-workflow" : cleaned;
    }
}

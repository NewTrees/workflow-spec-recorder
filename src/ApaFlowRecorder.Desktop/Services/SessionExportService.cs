using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Desktop.Services;

public sealed class SessionExportService
{
    private readonly MarkdownExporter _markdownExporter = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<string> ExportAsync(WorkflowSession session, CancellationToken cancellationToken = default)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var exportRoot = Path.Combine(documents, "ApaFlowRecorder", "Exports");
        Directory.CreateDirectory(exportRoot);

        var folderName = $"{SanitizeFileName(session.ProjectName)}-{DateTime.Now:yyyyMMdd-HHmmss}";
        var exportDirectory = Path.Combine(exportRoot, folderName);
        Directory.CreateDirectory(exportDirectory);

        var workflowJson = JsonSerializer.Serialize(session, _jsonOptions);
        await File.WriteAllTextAsync(Path.Combine(exportDirectory, "workflow.json"), workflowJson, cancellationToken);

        var markdown = _markdownExporter.Export(session);
        await File.WriteAllTextAsync(Path.Combine(exportDirectory, "APA需求文档.md"), markdown, cancellationToken);

        var screenshotPaths = session.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step.ScreenshotPath) && File.Exists(step.ScreenshotPath))
            .Select(step => step.ScreenshotPath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (screenshotPaths.Count > 0)
        {
            var assetDirectory = Path.Combine(exportDirectory, "assets");
            Directory.CreateDirectory(assetDirectory);
            foreach (var screenshotPath in screenshotPaths)
            {
                var destination = Path.Combine(assetDirectory, Path.GetFileName(screenshotPath));
                File.Copy(screenshotPath, destination, overwrite: true);
            }
        }

        return exportDirectory;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "未命名流程" : cleaned;
    }
}

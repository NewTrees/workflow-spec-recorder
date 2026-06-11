using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class RecordingPackageMaterialCollectorTests
{
    [Fact]
    public async Task Collect_returns_workflow_markdown_and_screenshot_assets_from_export_directory()
    {
        var exportDirectory = Path.Combine(Path.GetTempPath(), $"apa-export-{Guid.NewGuid():N}");
        var assetsDirectory = Path.Combine(exportDirectory, "assets");
        Directory.CreateDirectory(assetsDirectory);
        var workflowPath = Path.Combine(exportDirectory, "workflow.json");
        var markdownPath = Path.Combine(exportDirectory, "APA需求文档.md");
        var screenshotPath = Path.Combine(assetsDirectory, "step-001.png");
        var ignoredPath = Path.Combine(exportDirectory, "debug.tmp");
        await File.WriteAllTextAsync(workflowPath, "{}");
        await File.WriteAllTextAsync(markdownPath, "# 需求");
        await File.WriteAllBytesAsync(screenshotPath, [1, 2, 3]);
        await File.WriteAllTextAsync(ignoredPath, "ignore");

        try
        {
            var paths = new RecordingPackageMaterialCollector().Collect(exportDirectory);

            Assert.Equal(
                [workflowPath, markdownPath, screenshotPath],
                paths);
        }
        finally
        {
            Directory.Delete(exportDirectory, recursive: true);
        }
    }
}

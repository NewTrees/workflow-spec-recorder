namespace ApaFlowRecorder.Core.Services;

public sealed class RecordingPackageMaterialCollector
{
    private static readonly string[] RootMaterialFileNames =
    [
        "workflow.json",
        "APA需求文档.md"
    ];

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    public IReadOnlyList<string> Collect(string exportDirectory)
    {
        if (string.IsNullOrWhiteSpace(exportDirectory) || !Directory.Exists(exportDirectory))
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var fileName in RootMaterialFileNames)
        {
            var path = Path.Combine(exportDirectory, fileName);
            if (File.Exists(path))
            {
                paths.Add(path);
            }
        }

        var assetsDirectory = Path.Combine(exportDirectory, "assets");
        if (!Directory.Exists(assetsDirectory))
        {
            return paths;
        }

        paths.AddRange(Directory
            .EnumerateFiles(assetsDirectory)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        return paths;
    }
}

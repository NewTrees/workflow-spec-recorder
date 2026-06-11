using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class OfficeMaterialReader
{
    private const int MaxEmbeddedImagesPerOfficeFile = 40;
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private readonly string _embeddedImageRoot;

    public OfficeMaterialReader(string? embeddedImageRoot = null)
    {
        _embeddedImageRoot = embeddedImageRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ApaFlowRecorder",
            "material-images");
    }

    public SourceMaterialFile ReadMaterial(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var material = new SourceMaterialFile
        {
            Path = path,
            FileName = Path.GetFileName(path),
            Kind = extension.TrimStart('.')
        };

        try
        {
            switch (extension)
            {
                case ".xlsx":
                    material.Workbook = ReadWorkbook(path);
                    material.TextPreview = DescribeWorkbook(material.Workbook);
                    break;
                case ".docx":
                    material.Document = ReadDocument(path);
                    material.EmbeddedImages.AddRange(ExtractEmbeddedImages(path, "word/media/", "Word"));
                    material.TextPreview = material.Document.Text;
                    AppendEmbeddedImageStatus(material);
                    break;
                case ".pptx":
                    material.Presentation = ReadPresentation(path);
                    material.EmbeddedImages.AddRange(ExtractEmbeddedImages(path, "ppt/media/", "PPT"));
                    material.TextPreview = string.Join(Environment.NewLine, material.Presentation.Slides.Select(slide => slide.Text));
                    AppendEmbeddedImageStatus(material);
                    break;
                case ".txt":
                case ".md":
                case ".csv":
                case ".json":
                case ".xml":
                case ".html":
                case ".htm":
                case ".log":
                    material.TextPreview = ReadPlainText(path);
                    break;
                case ".pdf":
                    material.Status = "已加入；当前版本仅读取 PDF 文件名和大小，正文建议另存为 Word/TXT 后加入";
                    material.TextPreview = DescribeFile(path);
                    break;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".webp":
                    material.Status = "已加入；图片会作为视觉证据发送给支持图像输入的大模型";
                    material.TextPreview = DescribeFile(path);
                    break;
                default:
                    material.Status = "已加入；当前版本仅读取文件名和大小";
                    material.TextPreview = DescribeFile(path);
                    break;
            }
        }
        catch (Exception ex)
        {
            material.Status = $"读取失败：{ex.Message}";
        }

        return material;
    }

    public WorkbookSummary ReadWorkbook(string path, int previewRowLimit = 8)
    {
        using var archive = ZipFile.OpenRead(path);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetTargets = ReadSheetTargets(archive);
        var workbook = ReadWorkbookSheets(archive);

        var summary = new WorkbookSummary { FileName = Path.GetFileName(path) };
        foreach (var sheet in workbook)
        {
            if (!sheetTargets.TryGetValue(sheet.RelationshipId, out var target))
            {
                continue;
            }

            var normalizedTarget = target.Replace("\\", "/");
            if (!normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTarget = "xl/" + normalizedTarget.TrimStart('/');
            }

            var entry = archive.GetEntry(normalizedTarget);
            if (entry is null)
            {
                continue;
            }

            summary.Worksheets.Add(ReadWorksheet(entry, sheet.Name, sharedStrings, previewRowLimit));
        }

        return summary;
    }

    public DocumentSummary ReadDocument(string path, int maxCharacters = 12000)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidOperationException("DOCX missing word/document.xml");
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var paragraphs = document.Descendants(Word + "p")
            .Select(p => string.Concat(p.Descendants(Word + "t").Select(t => t.Value)).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        var text = string.Join(Environment.NewLine, paragraphs);
        if (text.Length > maxCharacters)
        {
            text = text[..maxCharacters] + Environment.NewLine + "... [truncated]";
        }

        return new DocumentSummary
        {
            FileName = Path.GetFileName(path),
            Text = text,
            Paragraphs = paragraphs.Take(200).ToList()
        };
    }

    public PresentationSummary ReadPresentation(string path, int maxSlides = 80, int maxCharactersPerSlide = 2000)
    {
        using var archive = ZipFile.OpenRead(path);
        var slideEntries = archive.Entries
            .Where(entry => Regex.IsMatch(entry.FullName, @"^ppt/slides/slide\d+\.xml$", RegexOptions.IgnoreCase))
            .OrderBy(entry => SlideNumber(entry.FullName))
            .Take(maxSlides)
            .ToList();

        var summary = new PresentationSummary { FileName = Path.GetFileName(path) };
        foreach (var entry in slideEntries)
        {
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var text = string.Join(" ", document.Descendants(Drawing + "t").Select(node => node.Value.Trim()))
                .Trim();
            if (text.Length > maxCharactersPerSlide)
            {
                text = text[..maxCharactersPerSlide] + " ... [truncated]";
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                summary.Slides.Add(new SlideSummary
                {
                    Number = SlideNumber(entry.FullName),
                    Text = text
                });
            }
        }

        return summary;
    }

    private List<SourceMaterialImage> ExtractEmbeddedImages(string sourcePath, string mediaPrefix, string labelPrefix)
    {
        using var archive = ZipFile.OpenRead(sourcePath);
        var imageEntries = archive.Entries
            .Where(entry => entry.Length > 0)
            .Where(entry => entry.FullName.StartsWith(mediaPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new
            {
                Entry = entry,
                MediaType = TryGetImageMediaType(entry.FullName)
            })
            .Where(item => item.MediaType is not null)
            .OrderBy(item => item.Entry.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxEmbeddedImagesPerOfficeFile)
            .ToList();

        if (imageEntries.Count == 0)
        {
            return [];
        }

        var destinationDirectory = Path.Combine(_embeddedImageRoot, BuildImageCacheFolderName(sourcePath));
        Directory.CreateDirectory(destinationDirectory);

        var images = new List<SourceMaterialImage>();
        foreach (var item in imageEntries.Select((value, index) => new { value, index }))
        {
            var sourceFileName = ZipEntryFileName(item.value.Entry.FullName);
            var extension = Path.GetExtension(sourceFileName).ToLowerInvariant();
            var destinationFileName = $"{item.index + 1:D2}-{SanitizeFileName(Path.GetFileNameWithoutExtension(sourceFileName))}{extension}";
            var destinationPath = Path.Combine(destinationDirectory, destinationFileName);

            item.value.Entry.ExtractToFile(destinationPath, overwrite: true);
            images.Add(new SourceMaterialImage
            {
                Path = destinationPath,
                Label = $"{labelPrefix} 内嵌图片：{Path.GetFileName(sourcePath)} / {sourceFileName}",
                MediaType = item.value.MediaType!
            });
        }

        return images;
    }

    private static void AppendEmbeddedImageStatus(SourceMaterialFile material)
    {
        if (material.EmbeddedImages.Count == 0)
        {
            return;
        }

        material.Status = $"已读取；已提取 {material.EmbeddedImages.Count} 张内嵌图片作为视觉证据";
    }

    private static string ReadPlainText(string path, int maxCharacters = 12000)
    {
        var text = File.ReadAllText(path);
        return text.Length > maxCharacters
            ? text[..maxCharacters] + Environment.NewLine + "... [truncated]"
            : text;
    }

    private static string DescribeWorkbook(WorkbookSummary workbook)
    {
        var lines = workbook.Worksheets.Select(sheet =>
            $"{sheet.Name}: {sheet.RowCount} 行, {sheet.ColumnCount} 列, 表头={string.Join(" | ", sheet.Headers)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeFile(string path)
    {
        var info = new FileInfo(path);
        return $"{info.Name}; {info.Length} bytes";
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(Spreadsheet + "si")
            .Select(si => string.Concat(si.Descendants(Spreadsheet + "t").Select(t => t.Value)))
            .ToList();
    }

    private static Dictionary<string, string> ReadSheetTargets(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidOperationException("XLSX missing workbook relationships");
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Root!
            .Elements(PackageRelationships + "Relationship")
            .Where(r => r.Attribute("Id") is not null && r.Attribute("Target") is not null)
            .ToDictionary(
                r => r.Attribute("Id")!.Value,
                r => r.Attribute("Target")!.Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private static List<(string Name, string RelationshipId)> ReadWorkbookSheets(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidOperationException("XLSX missing workbook.xml");
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(Spreadsheet + "sheet")
            .Select(sheet => (
                Name: sheet.Attribute("name")?.Value ?? "Sheet",
                RelationshipId: sheet.Attribute(Relationships + "id")?.Value ?? string.Empty))
            .Where(sheet => !string.IsNullOrWhiteSpace(sheet.RelationshipId))
            .ToList();
    }

    private static WorksheetSummary ReadWorksheet(ZipArchiveEntry entry, string sheetName, IReadOnlyList<string> sharedStrings, int previewRowLimit)
    {
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var rows = document.Descendants(Spreadsheet + "row").ToList();
        var previewRows = new List<List<string>>();
        var maxColumn = 0;

        foreach (var row in rows.Take(previewRowLimit))
        {
            var cellsByColumn = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(Spreadsheet + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var column = ColumnIndexFromReference(reference);
                maxColumn = Math.Max(maxColumn, column);
                cellsByColumn[column] = ReadCellValue(cell, sharedStrings);
            }

            if (cellsByColumn.Count == 0)
            {
                previewRows.Add([]);
                continue;
            }

            var rowValues = new List<string>();
            for (var column = 1; column <= cellsByColumn.Keys.Max(); column++)
            {
                rowValues.Add(cellsByColumn.TryGetValue(column, out var value) ? value : string.Empty);
            }
            previewRows.Add(rowValues);
        }

        var headers = previewRows.FirstOrDefault()?.Where(value => !string.IsNullOrWhiteSpace(value)).ToList() ?? [];
        var dimension = document.Descendants(Spreadsheet + "dimension").FirstOrDefault()?.Attribute("ref")?.Value;
        var (rowCount, columnCount) = ParseDimension(dimension);

        return new WorksheetSummary
        {
            Name = sheetName,
            RowCount = Math.Max(rowCount, rows.Count),
            ColumnCount = Math.Max(columnCount, maxColumn),
            Headers = headers,
            PreviewRows = previewRows
        };
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var cellType = cell.Attribute("t")?.Value;
        if (cellType == "inlineStr")
        {
            return string.Concat(cell.Descendants(Spreadsheet + "t").Select(t => t.Value)).Trim();
        }

        var value = cell.Element(Spreadsheet + "v")?.Value ?? string.Empty;
        if (cellType == "s" && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index].Trim();
        }

        return value.Trim();
    }

    private static int ColumnIndexFromReference(string reference)
    {
        var letters = Regex.Match(reference, "^[A-Z]+", RegexOptions.IgnoreCase).Value.ToUpperInvariant();
        if (letters.Length == 0)
        {
            return 1;
        }

        var index = 0;
        foreach (var letter in letters)
        {
            index = index * 26 + (letter - 'A' + 1);
        }
        return index;
    }

    private static (int Rows, int Columns) ParseDimension(string? dimension)
    {
        if (string.IsNullOrWhiteSpace(dimension))
        {
            return (0, 0);
        }

        var end = dimension.Split(':').Last();
        var rowText = Regex.Match(end, "[0-9]+$").Value;
        var rows = int.TryParse(rowText, out var rowCount) ? rowCount : 0;
        var columns = ColumnIndexFromReference(end);
        return (rows, columns);
    }

    private static string BuildImageCacheFolderName(string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
        return $"{baseName}-{info.Length}-{info.LastWriteTimeUtc.Ticks}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }

    private static string ZipEntryFileName(string fullName)
    {
        return fullName
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? "image";
    }

    private static string? TryGetImageMediaType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
    }

    private static int SlideNumber(string fullName)
    {
        var numberText = Regex.Match(fullName, @"slide(\d+)\.xml$", RegexOptions.IgnoreCase).Groups[1].Value;
        return int.TryParse(numberText, out var number) ? number : 0;
    }
}

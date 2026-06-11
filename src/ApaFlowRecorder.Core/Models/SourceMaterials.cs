namespace ApaFlowRecorder.Core.Models;

public sealed class SourceMaterialBundle
{
    public string? WorkbookPath { get; set; }
    public WorkbookSummary? Workbook { get; set; }
    public string? DocumentPath { get; set; }
    public DocumentSummary? Document { get; set; }
    public List<SourceMaterialFile> Files { get; set; } = [];

    public IEnumerable<WorkbookSummary> Workbooks =>
        Files.Where(file => file.Workbook is not null).Select(file => file.Workbook!)
            .Concat(Workbook is null ? [] : [Workbook]);

    public IEnumerable<DocumentSummary> Documents =>
        Files.Where(file => file.Document is not null).Select(file => file.Document!)
            .Concat(Document is null ? [] : [Document]);

    public IEnumerable<PresentationSummary> Presentations =>
        Files.Where(file => file.Presentation is not null).Select(file => file.Presentation!);

    public IEnumerable<SourceMaterialImage> EmbeddedImages =>
        Files.SelectMany(file => file.EmbeddedImages);
}

public sealed class SourceMaterialFile
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Kind { get; set; } = "unknown";
    public string Status { get; set; } = "已读取";
    public WorkbookSummary? Workbook { get; set; }
    public DocumentSummary? Document { get; set; }
    public PresentationSummary? Presentation { get; set; }
    public List<SourceMaterialImage> EmbeddedImages { get; set; } = [];
    public string TextPreview { get; set; } = string.Empty;
}

public sealed class SourceMaterialImage
{
    public string Path { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
}

public sealed class WorkbookSummary
{
    public string FileName { get; set; } = string.Empty;
    public List<WorksheetSummary> Worksheets { get; set; } = [];
}

public sealed class WorksheetSummary
{
    public string Name { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<string> Headers { get; set; } = [];
    public List<List<string>> PreviewRows { get; set; } = [];
}

public sealed class DocumentSummary
{
    public string FileName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<string> Paragraphs { get; set; } = [];
}

public sealed class PresentationSummary
{
    public string FileName { get; set; } = string.Empty;
    public List<SlideSummary> Slides { get; set; } = [];
}

public sealed class SlideSummary
{
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
}

using System.IO.Compression;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class OfficeMaterialReaderTests
{
    [Fact]
    public async Task ReadMaterial_extracts_docx_embedded_images_as_visual_evidence_files()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"apa-docx-{Guid.NewGuid():N}");
        var docxPath = Path.Combine(workspace, "图文需求.docx");
        var imageCache = Path.Combine(workspace, "cache");
        Directory.CreateDirectory(workspace);
        CreateDocxWithImage(docxPath, "流程说明", [1, 2, 3]);

        try
        {
            var material = new OfficeMaterialReader(imageCache).ReadMaterial(docxPath);

            Assert.NotNull(material.Document);
            Assert.Contains("流程说明", material.Document.Text);
            var image = Assert.Single(material.EmbeddedImages);
            Assert.Equal("image/png", image.MediaType);
            Assert.Contains("图文需求.docx", image.Label);
            Assert.True(File.Exists(image.Path));
            Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(image.Path));
            Assert.Contains("内嵌图片", material.Status);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task ReadMaterial_extracts_pptx_embedded_images_as_visual_evidence_files()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"apa-pptx-{Guid.NewGuid():N}");
        var pptxPath = Path.Combine(workspace, "图文说明.pptx");
        var imageCache = Path.Combine(workspace, "cache");
        Directory.CreateDirectory(workspace);
        CreatePptxWithImage(pptxPath, [4, 5, 6]);

        try
        {
            var material = new OfficeMaterialReader(imageCache).ReadMaterial(pptxPath);

            var image = Assert.Single(material.EmbeddedImages);
            Assert.Equal("image/jpeg", image.MediaType);
            Assert.True(File.Exists(image.Path));
            Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(image.Path));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static void CreateDocxWithImage(string path, string text, byte[] imageBytes)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "word/document.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body>
            </w:document>
            """);
        WriteBytes(archive, "word/media/image1.png", imageBytes);
    }

    private static void CreatePptxWithImage(string path, byte[] imageBytes)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "ppt/slides/slide1.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>截图说明</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld>
            </p:sld>
            """);
        WriteBytes(archive, "ppt/media/image1.jpg", imageBytes);
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    private static void WriteBytes(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}

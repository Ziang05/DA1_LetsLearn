using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;

namespace LetsLearn.UseCases.Services.AiQuestionGeneration
{
    public class DocumentTextExtractor : IDocumentTextExtractor
    {
        public async Task<ExtractedDocument> ExtractAsync(IFormFile file, string savedPath, CancellationToken ct = default)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return extension switch
            {
                ".txt" => await ExtractTextAsync(savedPath, ct),
                ".docx" => ExtractDocx(savedPath),
                ".pdf" => ExtractPdf(savedPath),
                _ => throw new NotSupportedException("Only .docx, .txt, and configured .pdf files are supported.")
            };
        }

        private static async Task<ExtractedDocument> ExtractTextAsync(string path, CancellationToken ct)
        {
            var text = await File.ReadAllTextAsync(path, ct);
            return new ExtractedDocument
            {
                Text = text,
                Blocks = new List<ExtractedDocumentBlock>
                {
                    new() { Text = text }
                }
            };
        }

        private static ExtractedDocument ExtractDocx(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            var documentEntry = archive.GetEntry("word/document.xml")
                ?? throw new InvalidOperationException("Invalid docx file: word/document.xml was not found.");

            using var stream = documentEntry.Open();
            var xml = XDocument.Load(stream);
            var ns = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            var blocks = new List<ExtractedDocumentBlock>();
            string? currentHeading = null;

            foreach (var paragraph in xml.Descendants(ns + "p"))
            {
                var text = string.Join(" ", paragraph.Descendants(ns + "t").Select(t => t.Value)).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var style = paragraph.Descendants(ns + "pStyle").FirstOrDefault()?.Attribute(ns + "val")?.Value;
                if (!string.IsNullOrWhiteSpace(style) && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                {
                    currentHeading = text;
                    blocks.Add(new ExtractedDocumentBlock { Heading = currentHeading, Text = text });
                    continue;
                }

                blocks.Add(new ExtractedDocumentBlock { Heading = currentHeading, Text = text });
            }

            foreach (var table in xml.Descendants(ns + "tbl"))
            {
                var rows = table.Descendants(ns + "tr")
                    .Select(row => string.Join(" | ", row.Descendants(ns + "tc")
                        .Select(cell => string.Join(" ", cell.Descendants(ns + "t").Select(t => t.Value)).Trim())
                        .Where(cellText => !string.IsNullOrWhiteSpace(cellText))))
                    .Where(rowText => !string.IsNullOrWhiteSpace(rowText));

                var tableText = string.Join(Environment.NewLine, rows);
                if (!string.IsNullOrWhiteSpace(tableText))
                {
                    blocks.Add(new ExtractedDocumentBlock { Heading = currentHeading, Text = tableText });
                }
            }

            var fullText = string.Join(Environment.NewLine, blocks.Select(b => b.Text));
            return new ExtractedDocument { Text = fullText, Blocks = blocks };
        }

        private static ExtractedDocument ExtractPdf(string path)
        {
            var blocks = new List<ExtractedDocumentBlock>();
            using var document = PdfDocument.Open(path);

            foreach (var page in document.GetPages())
            {
                var text = page.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    blocks.Add(new ExtractedDocumentBlock
                    {
                        Heading = $"Page {page.Number}",
                        Text = text,
                        PageNumber = page.Number
                    });
                }
            }

            var fullText = string.Join(Environment.NewLine, blocks.Select(b => b.Text));
            return new ExtractedDocument { Text = fullText, Blocks = blocks };
        }
    }

    public class DocumentChunkingService : IDocumentChunkingService
    {
        private const int TargetChars = 3200;
        private const int OverlapChars = 450;

        public List<LectureChunkDto> Chunk(ExtractedDocument document)
        {
            var chunks = new List<LectureChunkDto>();
            var buffer = new StringBuilder();
            string? heading = null;
            int order = 1;

            foreach (var block in document.Blocks.Where(b => !string.IsNullOrWhiteSpace(b.Text)))
            {
                if (!string.IsNullOrWhiteSpace(block.Heading))
                {
                    heading = block.Heading;
                }

                if (buffer.Length + block.Text.Length > TargetChars && buffer.Length > 0)
                {
                    AddChunk(chunks, buffer.ToString(), heading, order++);
                    var overlap = buffer.Length > OverlapChars
                        ? buffer.ToString(buffer.Length - OverlapChars, OverlapChars)
                        : buffer.ToString();
                    buffer.Clear();
                    buffer.AppendLine(overlap);
                }

                buffer.AppendLine(block.Text);
            }

            if (buffer.Length > 0)
            {
                AddChunk(chunks, buffer.ToString(), heading, order);
            }

            return chunks;
        }

        private static void AddChunk(List<LectureChunkDto> chunks, string content, string? heading, int order)
        {
            var normalized = content.Trim();
            if (normalized.Length == 0)
            {
                return;
            }

            chunks.Add(new LectureChunkDto
            {
                Id = Guid.NewGuid(),
                Heading = heading,
                Content = normalized,
                ChunkOrder = order
            });
        }
    }
}

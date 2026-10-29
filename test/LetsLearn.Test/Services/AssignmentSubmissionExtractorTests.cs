using System.Net;
using System.IO.Compression;
using System.Text;
using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services.AiAssignmentGrading;
using Microsoft.Extensions.Configuration;
using Moq;

namespace LetsLearn.Test.Services;

public sealed class AssignmentSubmissionExtractorTests
{
    [Fact]
    public async Task ExtractsTextFromConfiguredCloudinaryTxtFile()
    {
        var textExtractor = new Mock<IDocumentTextExtractor>();
        textExtractor.Setup(x => x.ExtractAsync("answer.txt", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument { Text = "  a useful answer\r\nwith   spacing  " });
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("answer"))
        });
        var extractor = CreateExtractor(handler, textExtractor.Object);

        var snapshot = await extractor.ExtractAsync(Submission(
            "answer.txt", "https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/answer.txt"));

        Assert.Equal("a useful answer\nwith spacing", snapshot.Text);
        Assert.Equal(1, snapshot.FileCount);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.ContentHash));
        textExtractor.Verify(x => x.ExtractAsync("answer.txt", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractsTextFromValidDocxDocument()
    {
        var documentBytes = CreateDocx("A DOCX answer for grading.");
        var extractor = CreateExtractor(OkHandler(documentBytes), new LetsLearn.UseCases.Services.AiQuestionGeneration.DocumentTextExtractor());

        var snapshot = await extractor.ExtractAsync(Submission(
            "answer.docx", "https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/answer.docx"));

        Assert.Equal("A DOCX answer for grading.", snapshot.Text);
        Assert.Equal(1, snapshot.FileCount);
    }

    [Theory]
    [InlineData("https://evil.example/demo-cloud/raw/upload/v1/LetsLearn/answer.txt")]
    [InlineData("http://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/answer.txt")]
    [InlineData("https://res.cloudinary.com/another-cloud/raw/upload/v1/LetsLearn/answer.txt")]
    [InlineData("https://res.cloudinary.com/demo-cloud/raw/upload/v1/other/answer.txt")]
    [InlineData("https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/answer.txt?next=evil")]
    public async Task RejectsUntrustedOrMalformedStorageUrls(string url)
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Network must not be called for an invalid URL."));
        var extractor = CreateExtractor(handler, Mock.Of<IDocumentTextExtractor>());

        var error = await Assert.ThrowsAsync<AssignmentSubmissionExtractionException>(() =>
            extractor.ExtractAsync(Submission("answer.txt", url)));

        Assert.Equal("invalid_storage_reference", error.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RejectsUnsupportedExtensionsBeforeNetworkAccess()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Network must not be called."));
        var extractor = CreateExtractor(handler, Mock.Of<IDocumentTextExtractor>());

        var error = await Assert.ThrowsAsync<AssignmentSubmissionExtractionException>(() => extractor.ExtractAsync(Submission(
            "program.cs", "https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/program.cs")));

        Assert.Equal("unsupported_file_type", error.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RejectsRedirectResponsesInsteadOfFollowingThem()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://evil.example/answer.txt");
            return response;
        });
        var extractor = CreateExtractor(handler, Mock.Of<IDocumentTextExtractor>());

        var error = await Assert.ThrowsAsync<AssignmentSubmissionExtractionException>(() => extractor.ExtractAsync(Submission(
            "answer.txt", "https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/answer.txt")));

        Assert.Equal("storage_redirect_rejected", error.Code);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RejectsFilesWhoseMagicBytesDoNotMatchTheirExtension()
    {
        var handler = OkHandler(Encoding.UTF8.GetBytes("not a pdf"));
        var extractor = CreateExtractor(handler, Mock.Of<IDocumentTextExtractor>());

        var error = await Assert.ThrowsAsync<AssignmentSubmissionExtractionException>(() => extractor.ExtractAsync(Submission(
            "answer.pdf", "https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/answer.pdf")));

        Assert.Equal("file_type_mismatch", error.Code);
    }

    [Fact]
    public async Task ReportsScannedPdfAsManualGradingCase()
    {
        var pdfHeader = Encoding.ASCII.GetBytes("%PDF-1.7\ncontent");
        var documentExtractor = new Mock<IDocumentTextExtractor>();
        documentExtractor.Setup(x => x.ExtractAsync("scan.pdf", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument { Text = string.Empty });
        var extractor = CreateExtractor(OkHandler(pdfHeader), documentExtractor.Object);

        var error = await Assert.ThrowsAsync<AssignmentSubmissionExtractionException>(() => extractor.ExtractAsync(Submission(
            "scan.pdf", "https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/scan.pdf")));

        Assert.Equal("pdf_has_no_text", error.Code);
    }

    [Fact]
    public async Task RejectsExtractedTextBeyondConfiguredLimit()
    {
        var documentExtractor = new Mock<IDocumentTextExtractor>();
        documentExtractor.Setup(x => x.ExtractAsync("answer.txt", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument { Text = "four" });
        var extractor = CreateExtractor(OkHandler(Encoding.UTF8.GetBytes("text")), documentExtractor.Object, maxTextLength: "3");

        var error = await Assert.ThrowsAsync<AssignmentSubmissionExtractionException>(() => extractor.ExtractAsync(Submission(
            "answer.txt", "https://res.cloudinary.com/demo-cloud/raw/upload/v1/LetsLearn/answer.txt")));

        Assert.Equal("extracted_text_too_long", error.Code);
    }

    [Fact]
    public async Task RejectsMissingSubmissionFiles()
    {
        var extractor = CreateExtractor(OkHandler(Array.Empty<byte>()), Mock.Of<IDocumentTextExtractor>());
        var error = await Assert.ThrowsAsync<AssignmentSubmissionExtractionException>(() => extractor.ExtractAsync(new AssignmentResponse()));
        Assert.Equal("submission_file_missing", error.Code);
    }

    private static AssignmentSubmissionExtractor CreateExtractor(StubHandler handler, IDocumentTextExtractor documentExtractor, string? maxTextLength = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AssignmentSubmissionStorage"))
            .Returns(new HttpClient(handler, disposeHandler: false));
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(c => c["Cloudinary:CloudName"]).Returns("demo-cloud");
        if (maxTextLength != null) configuration.SetupGet(c => c["AIGrading:MaxExtractedTextLength"]).Returns(maxTextLength);
        return new AssignmentSubmissionExtractor(factory.Object, documentExtractor, configuration.Object);
    }

    private static StubHandler OkHandler(byte[] content) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(content)
    });

    private static byte[] CreateDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>{System.Security.SecurityElement.Escape(text)}</w:t></w:r></w:p></w:body></w:document>");
        }
        return stream.ToArray();
    }

    private static AssignmentResponse Submission(string name, string url) => new()
    {
        Id = Guid.NewGuid(),
        TopicId = Guid.NewGuid(),
        StudentId = Guid.NewGuid(),
        Files = new List<CloudinaryFile> { new() { Id = Guid.NewGuid(), Name = name, DownloadUrl = url } }
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }
}

using LetsLearn.UseCases.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace LetsLearn.Test.Services;

public class MediaFilePolicyTests
{
    [Theory]
    [InlineData("Bài làm.ZIP", "504B0304")]
    [InlineData("empty.zip", "504B0506")]
    [InlineData("split.zip", "504B0708")]
    [InlineData("assignment.rar", "526172211A0700")]
    [InlineData("assignment.RAR", "526172211A070100")]
    [InlineData("assignment.7z", "377ABCAF271C")]
    public async Task AcceptsArchiveSignaturesWithoutRelyingOnMimeType(string name, string hex)
    {
        var bytes = Convert.FromHexString(hex);
        var file = new InMemoryFormFile(bytes, name, "application/octet-stream");

        Assert.Equal(name, await MediaFilePolicy.ValidateAsync(file));
        // Validation opens a separate stream; the subsequent upload still starts at byte zero.
        using var upload = file.OpenReadStream();
        Assert.Equal(bytes[0], upload.ReadByte());
    }

    [Theory]
    [InlineData("fake.zip", "4D5A0000")]
    [InlineData("wrong.rar", "504B0304")]
    [InlineData("truncated.7z", "377A")]
    public async Task RejectsMismatchedOrTruncatedArchiveHeaders(string name, string hex)
    {
        var bytes = Convert.FromHexString(hex);
        var file = new InMemoryFormFile(bytes, name, "application/octet-stream");
        await Assert.ThrowsAsync<ArgumentException>(() => MediaFilePolicy.ValidateAsync(file));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MediaFilePolicy.MaxFileSize + 1)]
    public async Task RejectsInvalidLengthBeforeOpeningStream(long length)
    {
        var file = new Mock<IFormFile>(MockBehavior.Strict);
        file.SetupGet(f => f.Length).Returns(length);
        await Assert.ThrowsAsync<ArgumentException>(() => MediaFilePolicy.ValidateAsync(file.Object));
    }

    [Fact]
    public async Task KeepsOrdinaryDocumentsAndSanitizesClientPath()
    {
        var file = new InMemoryFormFile(new byte[] { 1 }, @"C:\fakepath\Bài làm.pdf", "application/pdf");
        Assert.Equal("Bài làm.pdf", await MediaFilePolicy.ValidateAsync(file));
    }

    [Fact]
    public async Task AcceptsExactSizeLimit()
    {
        var file = new Mock<IFormFile>(MockBehavior.Strict);
        file.SetupGet(f => f.Length).Returns(MediaFilePolicy.MaxFileSize);
        file.SetupGet(f => f.FileName).Returns("document.pdf");
        Assert.Equal("document.pdf", await MediaFilePolicy.ValidateAsync(file.Object));
    }
}

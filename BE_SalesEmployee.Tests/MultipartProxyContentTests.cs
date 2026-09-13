using System.Net.Http.Headers;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class MultipartProxyContentTests
{
    [Fact]
    public void Empty_ContentType_Throws_With_MediaTypeHeaderValue_NullCoalesce_Pattern()
    {
        // Reproduces the gateway bug: "" ?? "application/octet-stream" stays ""
        var contentType = "" ?? "application/octet-stream";
        Assert.ThrowsAny<Exception>(() => new MediaTypeHeaderValue(contentType));
    }

    [Theory]
    [InlineData(null, "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    [InlineData("   ", "application/octet-stream")]
    [InlineData("image/jpeg", "image/jpeg")]
    [InlineData(" image/png ", "image/png")]
    public void NormalizeContentType_Never_Returns_Empty(string? input, string expected)
    {
        Assert.Equal(expected, MultipartProxyContent.NormalizeContentType(input));
    }

    [Fact]
    public async Task FromFormFileAsync_Accepts_Empty_ContentType()
    {
        var file = new FormFileStub(new byte[] { 1, 2, 3 }, "shop.jpg", contentType: "");
        using var form = await MultipartProxyContent.FromFormFileAsync(file);
        Assert.NotNull(form);
        Assert.Single(form);
    }

    private sealed class FormFileStub : IFormFile
    {
        private readonly byte[] _bytes;

        public FormFileStub(byte[] bytes, string fileName, string contentType)
        {
            _bytes = bytes;
            FileName = fileName;
            ContentType = contentType;
            Name = "file";
            Length = bytes.Length;
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; }
        public string Name { get; }
        public string FileName { get; }

        public void CopyTo(Stream target) => target.Write(_bytes);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            target.Write(_bytes);
            return Task.CompletedTask;
        }

        public Stream OpenReadStream() => new MemoryStream(_bytes, writable: false);
    }
}

using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DirectorAssignedTaskFileStorageTests : IDisposable
{
    private readonly FakeEnv _env = new();
    private readonly DirectorAssignedTaskFileStorage _storage;

    public DirectorAssignedTaskFileStorageTests()
    {
        _storage = new DirectorAssignedTaskFileStorage(
            _env,
            NullLogger<DirectorAssignedTaskFileStorage>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_env.ContentRootPath))
                Directory.Delete(_env.ContentRootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("banner.png", "image/png")]
    [InlineData("anim.gif", "image/gif")]
    [InlineData("shot.webp", "image/webp")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("brief.doc", "application/msword")]
    [InlineData("brief.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("sheet.xls", "application/vnd.ms-excel")]
    [InlineData("sheet.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task SaveAsync_allows_image_pdf_word_excel_extensions(string fileName, string contentType)
    {
        var taskId = Guid.NewGuid();
        var file = new FormFileStub(new byte[] { 1, 2, 3, 4 }, fileName, contentType);

        var (ok, error, storedName) = await _storage.SaveAsync(taskId, file, CancellationToken.None);

        Assert.True(ok, error);
        Assert.Null(error);
        Assert.False(string.IsNullOrWhiteSpace(storedName));
        Assert.True(File.Exists(Path.Combine(_storage.TaskDir(taskId), storedName!)));
    }

    [Theory]
    [InlineData("payload.exe", "application/octet-stream")]
    [InlineData("script.js", "text/javascript")]
    [InlineData("note.txt", "text/plain")]
    [InlineData("archive.zip", "application/zip")]
    [InlineData("page.html", "text/html")]
    [InlineData("noext", "application/octet-stream")]
    public async Task SaveAsync_rejects_disallowed_extensions(string fileName, string contentType)
    {
        var taskId = Guid.NewGuid();
        var file = new FormFileStub(new byte[] { 1, 2, 3 }, fileName, contentType);

        var (ok, error, storedName) = await _storage.SaveAsync(taskId, file, CancellationToken.None);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Null(storedName);
        Assert.False(Directory.Exists(_storage.TaskDir(taskId)) &&
                     Directory.EnumerateFiles(_storage.TaskDir(taskId)).Any());
    }

    [Fact]
    public async Task SaveAsync_stores_safe_guid_filename_with_sanitized_extension()
    {
        var taskId = Guid.NewGuid();
        var file = new FormFileStub(
            new byte[] { 9, 8, 7 },
            @"..\..\evil.PDF",
            "application/pdf");

        var (ok, error, storedName) = await _storage.SaveAsync(taskId, file, CancellationToken.None);

        Assert.True(ok, error);
        Assert.NotNull(storedName);
        Assert.DoesNotContain("..", storedName, StringComparison.Ordinal);
        Assert.DoesNotContain('/', storedName);
        Assert.DoesNotContain('\\', storedName);
        Assert.Equal(".pdf", Path.GetExtension(storedName), StringComparer.Ordinal);
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(storedName), "N", out _));
        Assert.Equal(storedName, Path.GetFileName(storedName));
    }

    [Fact]
    public async Task Resolve_returns_path_and_content_type_for_saved_file()
    {
        var taskId = Guid.NewGuid();
        var file = new FormFileStub(new byte[] { 5, 5, 5 }, "report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var (ok, _, storedName) = await _storage.SaveAsync(taskId, file, CancellationToken.None);
        Assert.True(ok);

        var resolved = _storage.Resolve(taskId, storedName);

        Assert.True(resolved.Ok);
        Assert.NotNull(resolved.FullPath);
        Assert.True(File.Exists(resolved.FullPath));
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            resolved.ContentType);
    }

    [Fact]
    public void Resolve_rejects_path_traversal_file_names()
    {
        var taskId = Guid.NewGuid();
        var resolved = _storage.Resolve(taskId, "../outside.pdf");

        Assert.False(resolved.Ok);
        Assert.Null(resolved.FullPath);
        Assert.Null(resolved.ContentType);
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } =
            Path.Combine(Path.GetTempPath(), "assigned-task-files-" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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

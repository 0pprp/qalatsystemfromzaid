using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace BE_SalesEmployee.Services;

/// <summary>
/// Builds multipart proxy payloads safely. Empty Content-Type must not reach MediaTypeHeaderValue.
/// </summary>
public static class MultipartProxyContent
{
    public static async Task<MultipartFormDataContent> FromFormFileAsync(
        IFormFile file,
        string fieldName = "file",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length <= 0)
        {
            throw new ArgumentException("Empty upload.", nameof(file));
        }

        var bytes = new byte[file.Length];
        await using (var input = file.OpenReadStream())
        {
            var read = 0;
            while (read < bytes.Length)
            {
                var n = await input.ReadAsync(bytes.AsMemory(read, bytes.Length - read), ct);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            if (read != bytes.Length)
            {
                Array.Resize(ref bytes, read);
            }
        }

        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(NormalizeContentType(file.ContentType));
        var fileName = string.IsNullOrWhiteSpace(file.FileName) ? "upload.bin" : file.FileName;
        var form = new MultipartFormDataContent();
        form.Add(part, fieldName, fileName);
        return form;
    }

    public static string NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
}

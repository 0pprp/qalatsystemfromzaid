using System.Text.Json;
using System.Text.Json.Nodes;

namespace BE_CustomerApp.Infrastructure;

/// <summary>Maps exceptions to the contract's error body: { error, code, ...extra }. Never leaks internals.</summary>
public sealed class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex) when (!context.Response.HasStarted)
        {
            await WriteAsync(context, ex.Status, ex.Code, ex.Message, ex.Extra);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to write.
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, 500, "server_error", "حدث خطأ غير متوقع، يرجى المحاولة لاحقاً", null);
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string code, string message, object? extra)
    {
        var body = new JsonObject { ["error"] = message, ["code"] = code };
        if (extra is not null && JsonSerializer.SerializeToNode(extra, Json) is JsonObject extraObj)
        {
            foreach (var (key, value) in extraObj.ToList())
            {
                extraObj.Remove(key);
                body[key] = value;
            }
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(body.ToJsonString(Json));
    }
}

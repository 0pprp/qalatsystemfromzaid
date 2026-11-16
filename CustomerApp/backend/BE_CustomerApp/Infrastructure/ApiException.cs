namespace BE_CustomerApp.Infrastructure;

/// <summary>Expected, user-facing failure. Message is Arabic and safe to show; Code is machine-readable.</summary>
public class ApiException : Exception
{
    public ApiException(int status, string code, string message, object? extra = null) : base(message)
    {
        Status = status;
        Code = code;
        Extra = extra;
    }

    public int Status { get; }
    public string Code { get; }
    public object? Extra { get; }

    public static ApiException BadRequest(string code, string message, object? extra = null) => new(400, code, message, extra);
    public static ApiException NotFound(string message = "العنصر غير موجود") => new(404, "not_found", message);
    public static ApiException Forbidden(string code, string message) => new(403, code, message);
    public static ApiException Conflict(string code, string message, object? extra = null) => new(409, code, message, extra);

    public static ApiException Validation(Dictionary<string, string> fields) =>
        new(400, "validation", "يرجى تصحيح الحقول المطلوبة", new { fields });
}

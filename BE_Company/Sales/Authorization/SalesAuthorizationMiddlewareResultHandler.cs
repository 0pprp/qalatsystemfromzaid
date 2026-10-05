using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace BE_Company.Sales.Authorization;

/// <summary>
/// ASP.NET Forbid/Challenge often returns empty body — the Flutter app then shows
/// a generic «تعذر بدء الدوام». Always return an Arabic message for sales policies.
/// </summary>
public sealed class SalesAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "انتهت الجلسة أو لم يتم تسجيل الدخول. أعد تسجيل الدخول."
            });
            return;
        }

        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var path = context.Request.Path.Value ?? "";
            var message = path.Contains("shifts", StringComparison.OrdinalIgnoreCase)
                ? "هذا الحساب غير مخوّل لبدء دوام موظف المبيعات. سجّل دخول بحساب نوعه «موظف مبيعات» فقط."
                : "ليس لديك صلاحية لتنفيذ هذا الإجراء بحسابك الحالي.";
            await context.Response.WriteAsJsonAsync(new { message });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

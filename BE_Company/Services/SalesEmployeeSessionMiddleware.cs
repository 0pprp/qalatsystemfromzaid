using BE_Company.IRepository;
using BE_Company.Sales.Authorization;

namespace BE_Company.Services
{
    public sealed class SalesEmployeeSessionMiddleware
    {
        private readonly RequestDelegate _next;

        public SalesEmployeeSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IUsersRepository users)
        {
            var user = context.User;
            if (user.Identity?.IsAuthenticated == true
                && !string.Equals(
                    user.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value,
                    SalesGatewayKeyHandler.AuthSourceGateway,
                    StringComparison.OrdinalIgnoreCase))
            {
                var userType = user.FindFirst("UserType")?.Value
                               ?? context.Items["UserType"]?.ToString();
                if (SalesEmployeeSession.AppliesTo(userType)
                    && int.TryParse(user.FindFirst("UserID")?.Value ?? context.Items["UserID"]?.ToString(), out var userId)
                    && userId > 0)
                {
                    try
                    {
                        var tokenVersion = SalesEmployeeSession.ParseClaim(
                            user.FindFirst(SalesEmployeeSession.ClaimName)?.Value);
                        var current = await users.GetSalesEmployeeSessionVersionAsync(userId);
                        if (!SalesEmployeeSession.IsCurrent(tokenVersion, current))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                message = SalesEmployeeSession.Message,
                                code = SalesEmployeeSession.Code
                            });
                            return;
                        }
                    }
                    catch
                    {
                        // Keep existing requests working if the column/read fails.
                    }
                }
            }

            await _next(context);
        }
    }
}

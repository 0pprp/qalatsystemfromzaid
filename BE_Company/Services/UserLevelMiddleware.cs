using System.Security.Claims;

namespace BE_Company.Services
{
    /// <summary>
    /// Copies identity claims into HttpContext.Items for legacy controllers.
    /// Only trusts an authenticated ClaimsPrincipal (JWT already validated by the auth middleware).
    /// Never parses unverified JWT payloads.
    /// </summary>
    public class UserLevelMiddleware
    {
        private readonly RequestDelegate _next;

        public UserLevelMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var user = context.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    CopyClaim(context, user, "UserID");
                    CopyClaim(context, user, "UserType");
                    CopyClaim(context, user, "VisitorID");
                }
            }
            catch
            {
                // Proceed; [Authorize] and downstream checks remain authoritative.
            }

            await _next(context);
        }

        private static void CopyClaim(HttpContext context, ClaimsPrincipal user, string claimType)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(value))
            {
                context.Items[claimType] = value;
            }
        }
    }
}

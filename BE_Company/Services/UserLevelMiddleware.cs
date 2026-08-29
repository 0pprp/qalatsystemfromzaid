using System.IdentityModel.Tokens.Jwt;

namespace BE_Company.Services
{
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
                if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    var bearerToken = authHeader.ToString().Replace("Bearer ", "").Trim();
                    if (!string.IsNullOrEmpty(bearerToken))
                    {
                        var handler = new JwtSecurityTokenHandler();
                        var token = handler.ReadJwtToken(bearerToken);
                        var UserID = token.Claims.FirstOrDefault(c => c.Type == "UserID")?.Value;
                        var UserType = token.Claims.FirstOrDefault(c => c.Type == "UserType")?.Value;
                        var VisitorID = token.Claims.FirstOrDefault(c => c.Type == "VisitorID")?.Value;

                        if (!string.IsNullOrEmpty(UserID))
                        {
                            context.Items["UserID"] = UserID;
                        }

                        if (!string.IsNullOrEmpty(UserType))
                        {
                            context.Items["UserType"] = UserType;
                        }

                        if (!string.IsNullOrEmpty(VisitorID))
                        {
                            context.Items["VisitorID"] = VisitorID;
                        }
                    }
                }
            }
            catch
            {
                // Proceed even if token parsing fails; [Authorize] will handle it
            }
            await _next(context);

        }
    }
}

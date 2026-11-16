using System.Text.Json;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Middleware;

/// <summary>
/// Committee roles only reach متابعة المندوبين / متابعة المبيعات / عمل المتابعين (without الجباية),
/// and only for their committee's branches.
/// </summary>
public sealed class CommitteeScopeMiddleware : IMiddleware
{
    private static readonly string[] AllowedPrefixes =
    [
        "/api/director/cities",
        "/api/director/delegate-followup",
        "/api/director/sales-followup",
        "/api/director/followers/ops",
        "/api/sales-manager/followers/",
        "/api/auth/me",
        "/api/archive-flow/",
    ];

    private readonly OversightAccountService _accounts;

    public CommitteeScopeMiddleware(OversightAccountService accounts)
    {
        _accounts = accounts;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var role = context.User.FindFirst("UserType")?.Value;
        if (!SalesRoles.IsCommittee(role))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        if (!IsAllowedPath(path))
        {
            await Deny(context, "هذا القسم غير متاح للجنة المراقبة");
            return;
        }

        var userName = context.User.FindFirst("UserName")?.Value;
        var cities = await _accounts.CitiesForUserAsync(userName, context.RequestAborted);
        if (cities is null)
        {
            await Deny(context, "حساب اللجنة غير موجود");
            return;
        }

        // The JWT carries the branch list from login time; the store is the truth, so a
        // supervisor's branch change applies on the next request, not after the token expires.
        RefreshAllowedCitiesClaim(context, cities);

        var requested = RequestedCities(context);
        if (requested.Count > 0 && requested.Any(city => !cities.Contains(city, StringComparer.OrdinalIgnoreCase)))
        {
            await Deny(context, "هذا الفرع خارج نطاق اللجنة");
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method))
        {
            var bodyCity = await ReadBodyCityAsync(context);
            if (!string.IsNullOrWhiteSpace(bodyCity)
                     && !cities.Contains(bodyCity, StringComparer.OrdinalIgnoreCase))
            {
                await Deny(context, "هذا الفرع خارج نطاق اللجنة");
                return;
            }
        }

        await next(context);
    }

    /// <summary>Allowed board paths, minus الجباية (follower collection lists/payments).</summary>
    public static bool IsAllowedPath(string path)
    {
        if (!AllowedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return false;
        var isCollection = path.StartsWith("/api/director/followers/ops", StringComparison.OrdinalIgnoreCase)
            && (path.EndsWith("/collection", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/collection-enabled", StringComparison.OrdinalIgnoreCase));
        return !isCollection;
    }

    private static void RefreshAllowedCitiesClaim(HttpContext context, IReadOnlyList<string> cities)
    {
        if (context.User.Identity is not System.Security.Claims.ClaimsIdentity identity)
            return;
        foreach (var stale in identity.FindAll(TokenService.AllowedFilterCitiesClaim).ToList())
            identity.RemoveClaim(stale);
        if (cities.Count > 0)
            identity.AddClaim(new System.Security.Claims.Claim(
                TokenService.AllowedFilterCitiesClaim, string.Join(',', cities)));
    }

    private static List<string> RequestedCities(HttpContext context)
    {
        var values = new List<string>();
        foreach (var key in new[] { "cityValue", "city" })
        {
            if (context.Request.Query.TryGetValue(key, out var query) && !string.IsNullOrWhiteSpace(query))
                values.Add(query.ToString().Trim());
            if (context.Request.RouteValues.TryGetValue(key, out var route) && route is string text && text.Trim().Length > 0)
                values.Add(text.Trim());
        }
        return values;
    }

    private static async Task<string?> ReadBodyCityAsync(HttpContext context)
    {
        if (context.Request.ContentLength is null or 0)
            return null;
        context.Request.EnableBuffering();
        context.Request.Body.Position = 0;
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var text = await reader.ReadToEndAsync(context.RequestAborted);
        context.Request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            foreach (var name in new[] { "cityValue", "CityValue", "targetCityValue", "TargetCityValue" })
            {
                if (doc.RootElement.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var value = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value.Trim();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private static async Task Deny(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message });
    }
}

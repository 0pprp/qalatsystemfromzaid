using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BE_PromotersApp.Auth;

public static class AuthSchemes
{
    public const string Customer = "Customer";
    public const string Gateway = "Gateway";
}

public static class AuthPolicies
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}

public static class AuthSetup
{
    public const string CustomerIssuer = "qalaa-promoters-app";
    public const string CustomerIdClaim = "puid";

    /// <summary>Dashboard roles allowed into admin/* (UserType claim of the central gateway JWT).
    /// «مسؤول المراقبين» is the current name of the central monitor; «المراقب» is its legacy name.</summary>
    public static readonly string[] AdminUserTypes = ["المدير", "مسؤول المراقبين", "المراقب"];

    public static IServiceCollection AddPromotersAuth(this IServiceCollection services, IConfiguration config)
    {
        var customerKey = RequireKey(config, "Jwt:Key");
        var gatewayKey = RequireKey(config, "GatewayJwt:Key");

        services.AddAuthentication(AuthSchemes.Customer)
            .AddJwtBearer(AuthSchemes.Customer, o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = CustomerIssuer,
                    ValidateAudience = true,
                    ValidAudience = CustomerIssuer,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(customerKey)),
                };
            })
            // Tokens issued by BE_SalesEmployee TokenService (HS256 with its Jwt:Key; no issuer/audience).
            .AddJwtBearer(AuthSchemes.Gateway, o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(gatewayKey)),
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(AuthPolicies.Customer, p => p
                .AddAuthenticationSchemes(AuthSchemes.Customer)
                .RequireAuthenticatedUser()
                .RequireClaim(CustomerIdClaim));
            o.AddPolicy(AuthPolicies.Admin, p => p
                .AddAuthenticationSchemes(AuthSchemes.Gateway)
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    !ctx.User.HasClaim(c => c.Type == CustomerIdClaim) &&
                    AdminUserTypes.Contains(ctx.User.FindFirst("UserType")?.Value)));
        });
        return services;
    }

    private static string RequireKey(IConfiguration config, string name)
    {
        var key = config[name];
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length < 32)
            throw new InvalidOperationException($"{name} must be configured (min 32 characters).");
        return key.Trim();
    }

    public static long CustomerId(this ClaimsPrincipal user) =>
        long.Parse(user.FindFirst(CustomerIdClaim)?.Value ?? throw new UnauthorizedAccessException());
}

/// <summary>The dashboard user performing an admin action (for audit + "decided by").</summary>
public sealed record AdminActor(string Name, string Role)
{
    public static AdminActor From(ClaimsPrincipal user) => new(
        user.FindFirst("UserName")?.Value is { Length: > 0 } n ? n : "غير معروف",
        user.FindFirst("UserType")?.Value ?? "");
}

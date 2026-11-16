using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Admin;
using BE_PromotersApp.Domain.Materials;
using BE_PromotersApp.Domain.Notifications;
using BE_PromotersApp.Domain.Orders;
using BE_PromotersApp.Domain.Products;
using BE_PromotersApp.Domain.Users;
using BE_PromotersApp.Domain.Wallet;
using BE_PromotersApp.Infrastructure;
using BE_PromotersApp.Integrations.Gateway;
using BE_PromotersApp.Integrations.Push;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    o.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    // Dates are UTC and always sent with "Z" so the app and dashboard show them in Iraq time.
    o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
// Marketing videos can be large (MaterialService.MaxBytes); the endpoint sets its own limit too.
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaterialService.MaxBytes + 1_000_000);

// Infrastructure
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<DbMigrator>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<PublicUrls>();
builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddSingleton<AppReleaseService>();
builder.Services.AddSingleton(Channel.CreateUnbounded<PushJob>(new UnboundedChannelOptions { SingleReader = true }));

// Phone notifications (Firebase project qalaa-agents). Without the settings only the in-app bell works.
if (!string.IsNullOrWhiteSpace(config["Push:Fcm:ServiceAccountFile"]))
    builder.Services.AddHttpClient<IPushSender, FcmPushSender>();
else
    builder.Services.AddSingleton<IPushSender, NullPushSender>();

// The company system (orders go to «طلبات البيع» through the Sales Gateway)
if (config.GetValue<bool>("Gateway:UseFake"))
    builder.Services.AddSingleton<IGatewayClient, FakeGatewayClient>();
else
    builder.Services.AddHttpClient<IGatewayClient, HttpGatewayClient>();
builder.Services.AddSingleton<BranchCatalog>();

// Domain
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdminUsersService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<MaterialService>();

builder.Services.AddHostedService<PushDispatcher>();
if (config.GetValue("Workers:Enabled", true))
{
    builder.Services.AddHostedService<OrderSyncWorker>();
    builder.Services.AddHostedService<CleanupWorker>();
}

builder.Services.AddPromotersAuth(config);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync("""{"error":"طلبات كثيرة، يرجى المحاولة بعد قليل","code":"rate_limited"}""", ct);
    };
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:AuthPerMinute", 20), Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("orders", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirst(AuthSetup.CustomerIdClaim)?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:OrdersPer10Minutes", 20), Window = TimeSpan.FromMinutes(10) }));
});

var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (allowedOrigins.Length > 0) p.WithOrigins(allowedOrigins);
    else if (builder.Environment.IsDevelopment()) p.SetIsOriginAllowed(_ => true);
    p.AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition");
}));

var app = builder.Build();

await app.Services.GetRequiredService<DbMigrator>().MigrateAsync();
if (app.Environment.IsDevelopment() && config.GetValue<bool>("Demo:Seed"))
    await DemoSeeder.SeedAsync(app.Services.GetRequiredService<Db>(), app.Logger);

app.UseMiddleware<ErrorHandlingMiddleware>();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (config.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    });
}

app.UseCors();

var mediaRoot = MediaStorage.Root(config, app.Environment);
Directory.CreateDirectory(mediaRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media",
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable";
        ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
    },
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;

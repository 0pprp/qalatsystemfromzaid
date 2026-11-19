using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Controllers.Admin;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Domain.Notifications;
using BE_CustomerApp.Domain.Orders;
using BE_CustomerApp.Domain.Users;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Gateway;
using BE_CustomerApp.Integrations.Push;
using BE_CustomerApp.Integrations.Sms;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    // Arabic text stays readable in responses (still valid, safely escaped JSON for HTML-sensitive chars).
    o.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    // Dates are UTC: always sent with "Z" so the app and dashboard show them in Iraq time correctly.
    o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// Infrastructure
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<DbMigrator>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<PublicUrls>();
builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddSingleton<AppReleaseService>();
builder.Services.AddScoped<BE_CustomerApp.Domain.Work.WorkTargetService>();
builder.Services.AddSingleton(Channel.CreateUnbounded<PushJob>(new UnboundedChannelOptions { SingleReader = true }));

// Integrations (each falls back to a safe local implementation when not configured)
if (config.GetValue<bool>("Gateway:UseFake"))
{
    builder.Services.AddSingleton<IGatewayClient, FakeGatewayClient>();
}
else
{
    builder.Services.AddHttpClient<IGatewayClient, HttpGatewayClient>();
}
builder.Services.AddSingleton<BranchCatalog>();

if (string.Equals(config["Sms:Provider"], "Http", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<ISmsSender, HttpSmsSender>();
else
    builder.Services.AddSingleton<ISmsSender, LogSmsSender>();

if (!string.IsNullOrWhiteSpace(config["Push:Fcm:ServiceAccountFile"]))
    builder.Services.AddHttpClient<IPushSender, FcmPushSender>();
else
    builder.Services.AddSingleton<IPushSender, NullPushSender>();

// Domain
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BE_CustomerApp.Domain.Work.DailyTargetService>();
builder.Services.AddScoped<BE_CustomerApp.Domain.Work.WorkService>();
builder.Services.AddScoped<BE_CustomerApp.Domain.Work.WorkAlertService>();
builder.Services.AddScoped<BE_CustomerApp.Domain.Work.ProspectService>();
builder.Services.AddScoped<AdminAgentsService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ReportsService>();

// Background jobs
builder.Services.AddHostedService<PushDispatcher>();
if (config.GetValue("Workers:Enabled", true))
{
    builder.Services.AddHostedService<OrderSyncWorker>();
    builder.Services.AddHostedService<CleanupWorker>();
    builder.Services.AddHostedService<WorkDayCloseWorker>();
    builder.Services.AddHostedService<WorkAlertWorker>();
}

builder.Services.AddCustomerAppAuth(config);

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
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:OrdersPer10Minutes", 10), Window = TimeSpan.FromMinutes(10) }));
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

var mediaRoot = MediaStorage.Root(config, app.Environment);
Directory.CreateDirectory(mediaRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable";
        ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
    },
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;

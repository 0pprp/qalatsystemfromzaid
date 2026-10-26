using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Sales.Stores;
using BE_SalesEmployee.Services;

var builder = WebApplication.CreateBuilder(args);

// Dapper cannot bind DateOnly parameters/results without an explicit handler.
DapperDateOnlyTypeHandler.Register();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
// Typed HttpClient is transient; city list cache uses IMemoryCache (singleton) — see AdminCitiesService.
builder.Services.AddHttpClient<AdminCitiesService>();
// Must exceed SalesManagerBranchAggregator excel/evaluate CancelAfter (90s).
// A 20s client timeout made multi-province evaluate return 502
// ("تعذر تقييم الطلبات في محافظة المصدر") under real catalog load.
builder.Services.AddHttpClient<BranchProxyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(BranchProxyTimeouts.HttpClientSeconds);
});
builder.Services.AddHttpClient("crm-bridge", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<SalesManagerAccountService>();
builder.Services.AddSingleton<DelegatedManagerAccountService>();
builder.Services.AddSingleton<MainFollowerAccountService>();
builder.Services.AddSingleton<DirectorAccountService>();
builder.Services.AddSingleton<MonitorAccountService>();
builder.Services.AddScoped<SalesFilterLoginService>();
builder.Services.AddSingleton<SalesDevelopmentGuard>();
builder.Services.AddScoped<IGlobalCustomerSearchService, GatewayGlobalCustomerSearchService>();
builder.Services.AddScoped<ISalesManagerBranchAggregator, SalesManagerBranchAggregator>();

// Delegated Manager + MainFollower central stores.
// Production must use SQL (no in-memory fallback for financial/ops continuity).
var gatewaySqlConfigured = SqlGatewayConnectionFactory.IsConfigured(builder.Configuration);
if (gatewaySqlConfigured)
{
    builder.Services.AddSingleton<SqlGatewayConnectionFactory>();
    builder.Services.AddScoped<ICentralComplaintsStore, SqlCentralComplaintsStore>();
    builder.Services.AddScoped<ISalesExceptionStore, SqlSalesExceptionStore>();
    builder.Services.AddScoped<ICustomerWaiverStore, SqlCustomerWaiverStore>();
    builder.Services.AddScoped<IMobileUpdateStore, SqlMobileUpdateStore>();
    builder.Services.AddScoped<IMobileTutorialLinkStore, SqlMobileTutorialLinkStore>();
    builder.Services.AddScoped<IMobileTutorialVideoStore, SqlMobileTutorialVideoStore>();
    builder.Services.AddScoped<IAppAnnouncementStore, SqlAppAnnouncementStore>();
    builder.Services.AddScoped<IAppCommitteeTaskStore, SqlAppCommitteeTaskStore>();
    builder.Services.AddScoped<IDirectorAssignedTaskStore, SqlDirectorAssignedTaskStore>();
    builder.Services.AddScoped<IPriceBoardStore, SqlPriceBoardStore>();
    builder.Services.AddScoped<IDirectorManagerNoteStore, SqlDirectorManagerNoteStore>();
    builder.Services.AddScoped<IBranchManagerAppUserStore, SqlBranchManagerAppUserStore>();
    builder.Services.AddScoped<IBmBranchVisitStore, SqlBmBranchVisitStore>();
    builder.Services.AddScoped<ISalesEmployeeAppBlockStore, SqlSalesEmployeeAppBlockStore>();
    builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Stores.ICommitteeStore,
        BE_SalesEmployee.MainFollower.Stores.SqlCommitteeStore>();
}
else if (builder.Environment.IsProduction())
{
    throw new InvalidOperationException(
        "ConnectionStrings:SalesGateway is required in Production. In-memory gateway stores are not allowed.");
}
else
{
    builder.Services.AddSingleton<ICentralComplaintsStore, InMemoryCentralComplaintsStore>();
    builder.Services.AddSingleton<ISalesExceptionStore, InMemorySalesExceptionStore>();
    builder.Services.AddSingleton<ICustomerWaiverStore, InMemoryCustomerWaiverStore>();
    builder.Services.AddSingleton<IMobileUpdateStore, InMemoryMobileUpdateStore>();
    builder.Services.AddSingleton<IMobileTutorialLinkStore, InMemoryMobileTutorialLinkStore>();
    builder.Services.AddSingleton<IMobileTutorialVideoStore, InMemoryMobileTutorialVideoStore>();
    builder.Services.AddSingleton<IAppAnnouncementStore, InMemoryAppAnnouncementStore>();
    builder.Services.AddSingleton<IAppCommitteeTaskStore, InMemoryAppCommitteeTaskStore>();
    builder.Services.AddSingleton<IDirectorAssignedTaskStore, InMemoryDirectorAssignedTaskStore>();
    builder.Services.AddSingleton<IPriceBoardStore, InMemoryPriceBoardStore>();
    builder.Services.AddSingleton<IDirectorManagerNoteStore, InMemoryDirectorManagerNoteStore>();
    builder.Services.AddSingleton<IBranchManagerAppUserStore, InMemoryBranchManagerAppUserStore>();
    builder.Services.AddSingleton<IBmBranchVisitStore, InMemoryBmBranchVisitStore>();
    builder.Services.AddSingleton<ISalesEmployeeAppBlockStore, InMemorySalesEmployeeAppBlockStore>();
    builder.Services.AddSingleton<BE_SalesEmployee.MainFollower.Stores.ICommitteeStore,
        BE_SalesEmployee.MainFollower.Stores.InMemoryCommitteeStore>();
}

// Reviews/shifts: SQL when gateway configured; otherwise in-memory only outside Production.
if (gatewaySqlConfigured)
{
    builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Stores.ISaleReviewStore,
        BE_SalesEmployee.MainFollower.Stores.SqlSaleReviewStore>();
    builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Stores.IStaffReviewStore,
        BE_SalesEmployee.MainFollower.Stores.SqlStaffReviewStore>();
    builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Stores.IMainFollowerShiftStore,
        BE_SalesEmployee.MainFollower.Stores.SqlMainFollowerShiftStore>();
}
else
{
    builder.Services.AddSingleton<BE_SalesEmployee.MainFollower.Stores.ISaleReviewStore,
        BE_SalesEmployee.MainFollower.Stores.InMemorySaleReviewStore>();
    builder.Services.AddSingleton<BE_SalesEmployee.MainFollower.Stores.IStaffReviewStore,
        BE_SalesEmployee.MainFollower.Stores.InMemoryStaffReviewStore>();
    builder.Services.AddSingleton<BE_SalesEmployee.MainFollower.Stores.IMainFollowerShiftStore,
        BE_SalesEmployee.MainFollower.Stores.InMemoryMainFollowerShiftStore>();
}
builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Services.CommitteeService>();
builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Services.SaleReviewService>();
builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Services.StaffReviewService>();
builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Services.CommitteeHistoryService>();
builder.Services.AddScoped<BE_SalesEmployee.MainFollower.Services.MainFollowerShiftService>();

builder.Services.AddSingleton<IntentRecordingBranchNotePoster>();
builder.Services.AddScoped<IBranchNotePoster, GatewayBranchNotePoster>();
builder.Services.AddScoped<IBranchExceptionHoldPoster, GatewayExceptionHoldPoster>();
builder.Services.AddScoped<IBranchWaiverApplyPoster, GatewayWaiverApplyPoster>();
builder.Services.AddScoped<CentralComplaintsService>();
builder.Services.AddScoped<SalesExceptionService>();
builder.Services.AddScoped<CustomerWaiverService>();
builder.Services.AddScoped<MobileUpdateService>();
builder.Services.AddScoped<MobileTutorialLinkService>();
builder.Services.AddScoped<MobileTutorialVideoService>();
builder.Services.AddSingleton<AppAnnouncementImageStorage>();
builder.Services.AddScoped<AppAnnouncementService>();
builder.Services.AddScoped<AppCommitteeTaskService>();
builder.Services.AddSingleton<DirectorAssignedTaskFileStorage>();
builder.Services.AddScoped<DirectorAssignedTaskService>();
builder.Services.AddSingleton<BmBranchVisitFileStorage>();
builder.Services.AddScoped<BmBranchVisitService>();
builder.Services.AddScoped<PriceBoardService>();
builder.Services.AddScoped<DirectorManagerNotesService>();
builder.Services.AddScoped<BranchManagerAppUserService>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<IAuthorizationHandler, SalesRoleHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SalesPolicies.AnySales, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesEmployee, SalesRoles.SalesManager)));
    options.AddPolicy(SalesPolicies.SalesEmployee, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesEmployee)));
    options.AddPolicy(SalesPolicies.SalesManager, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
    options.AddPolicy(SalesPolicies.SalesFilterEmployee, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesFilterEmployee)));
    options.AddPolicy(SalesPolicies.DelegatedManager, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.DelegatedManager)));
    options.AddPolicy(SalesPolicies.MainFollower, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.MainFollower)));
    options.AddPolicy(SalesPolicies.Director, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.Director)));
    options.AddPolicy(SalesPolicies.Monitor, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.Monitor)));
    options.AddPolicy(SalesPolicies.BranchManager, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.BranchManager)));
    options.AddPolicy(SalesPolicies.DirectorOrMonitor, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.Director, SalesRoles.Monitor)));
    options.AddPolicy(SalesPolicies.SalesManagerOrDelegatedManager, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager, SalesRoles.DelegatedManager)));
    options.AddPolicy(SalesPolicies.SalesManagerOrDirector, p =>
        p.Requirements.Add(new SalesRoleRequirement(
            SalesRoles.SalesManager, SalesRoles.Director, SalesRoles.Monitor)));
    options.AddPolicy(SalesPolicies.ReadGps, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
    options.AddPolicy(SalesPolicies.ReadOtherSalesEmployees, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
});

var jwtKey = TokenService.RequireJwtKey(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Prefer Authorization header. Query access_token only for SignalR hubs.
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
    options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? Array.Empty<string>();
    options.AddPolicy("free", policy =>
    {
        if (builder.Environment.IsDevelopment() || origins.Length == 0)
        {
            // Development / unset: permissive for local demos. Production must set Cors:AllowedOrigins.
            if (builder.Environment.IsProduction() && origins.Length == 0)
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins must be configured in Production. CORS is not authorization.");
            }

            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BE_SalesEmployee", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Sales DB: {Catalog}", SalesDevelopmentGuard.AllowedDemoDatabase);
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<BE_SalesEmployee.Middleware.CorrelationIdMiddleware>();
app.UseCors("free");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<BE_SalesEmployee.Hubs.SalesTrackingHub>("/hubs/sales-tracking");
app.Run();

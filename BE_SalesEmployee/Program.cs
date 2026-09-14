using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<AdminCitiesService>();
builder.Services.AddHttpClient<BranchProxyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<SalesManagerAccountService>();
builder.Services.AddScoped<SalesFilterLoginService>();
builder.Services.AddSingleton<SalesDevelopmentGuard>();
builder.Services.AddScoped<IGlobalCustomerSearchService, GatewayGlobalCustomerSearchService>();
builder.Services.AddScoped<ISalesManagerBranchAggregator, SalesManagerBranchAggregator>();
builder.Services.AddScoped<IGlobalSalesManagerOrchestrator, GlobalSalesManagerOrchestrator>();
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
    options.AddPolicy(SalesPolicies.ReadGps, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
    options.AddPolicy(SalesPolicies.ReadOtherSalesEmployees, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
    options.AddPolicy("Company.MainAccountant", p =>
        p.RequireAssertion(ctx =>
            string.Equals(
                ctx.User.FindFirst("UserType")?.Value,
                "محاسب رئيسي",
                StringComparison.Ordinal)));
});

const string companyJwtScheme = "CompanyJwt";
var gatewayJwtKey = builder.Configuration["Jwt:Key"] ?? "SalesEmployeeGwSigningKey-2026-ChangeMe!!";
IReadOnlyList<SecurityKey> companyKeys;
try
{
    companyKeys = CompanyJwtSigningConfig.LoadRequired(builder.Configuration);
}
catch (InvalidOperationException ex)
{
    throw new InvalidOperationException(ex.Message, ex);
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
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
            // Gateway-issued tokens only — never accept company keys here for SM APIs.
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(gatewayJwtKey))
        };
    })
    .AddJwtBearer(companyJwtScheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            // Main Accountant tokens from BE_Company only — gateway Jwt:Key is NOT trusted.
            IssuerSigningKeys = companyKeys
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("free", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
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
}
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("free");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<BE_SalesEmployee.Hubs.SalesTrackingHub>("/hubs/sales-tracking");
app.Run();

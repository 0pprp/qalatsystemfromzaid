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
builder.Services.AddSingleton<SalesDevelopmentGuard>();
builder.Services.AddScoped<IGlobalCustomerSearchService, GatewayGlobalCustomerSearchService>();
builder.Services.AddScoped<ISalesManagerBranchAggregator, SalesManagerBranchAggregator>();
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
    options.AddPolicy(SalesPolicies.ReadGps, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
    options.AddPolicy(SalesPolicies.ReadOtherSalesEmployees, p =>
        p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "SalesEmployeeGwSigningKey-2026-ChangeMe!!";
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
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

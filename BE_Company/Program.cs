using BE_Company.Sales;
using BE_Company.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.RegisterRepositories();
builder.Services.AddSalesManagementModule();
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.AddCustomCors(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var salesGuard = scope.ServiceProvider.GetRequiredService<BE_Company.Sales.Services.SalesDevelopmentGuard>();
    salesGuard.LogDevelopmentCatalog();
}

// Configure the HTTP request pipeline.
app.UseGlobalExceptionHandler();
// wwwroot only. App_Data/sales PDFs are never served as static files.
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/App_Data", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/Sale_", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

// Swagger should be available in all environments for now to help debug
app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection(); // Commented out to allow HTTP access as requested

app.UseRouting();

// UseCors MUST be called after UseRouting and before UseAuthentication/UseAuthorization
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Middleware to extract user info from JWT
app.UseMiddleware<UserLevelMiddleware>();

app.MapControllers();
app.MapHub<BE_Company.Sales.Services.SalesTrackingHub>("/hubs/sales-tracking");

app.Run();

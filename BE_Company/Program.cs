using BE_Company.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.RegisterRepositories();
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.AddCustomCors(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseGlobalExceptionHandler();
app.UseStaticFiles();

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

app.Run();

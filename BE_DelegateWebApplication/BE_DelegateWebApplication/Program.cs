using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Repository;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(configuration =>
    {
        configuration.WithOrigins(builder.Configuration["allowedOrigins"]!).AllowAnyMethod()
        .AllowAnyHeader();
    });

    options.AddPolicy("free", configuration =>
    {
        configuration.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddScoped<ICustomersPaymentsRepository, CustomersPaymentsRepository>();
builder.Services.AddScoped<ICustomersPaymentsRequestsRepository, CustomersPaymentsRequestsRepository>();
builder.Services.AddScoped<ICustomersRepository, CustomersRepository>();
builder.Services.AddScoped<ICustomersSalesRepository, CustomersSalesRepository>();
builder.Services.AddScoped<IDelegateRepository, DelegateRepository>();
builder.Services.AddScoped<ITrustReceiptRepository, TrustReceiptRepository>();
builder.Services.AddScoped<IFollowerActionsRepository, FollowerActionsRepository>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.ISharedCustomerNotesService, BE_DelegateWebApplication.Services.SharedCustomerNotesService>();
        builder.Services.AddScoped<BE_DelegateWebApplication.Services.IDelegateComplaintsService, BE_DelegateWebApplication.Services.DelegateComplaintsService>();
        builder.Services.AddScoped<BE_DelegateWebApplication.Services.IFollowerComplaintsService, BE_DelegateWebApplication.Services.FollowerComplaintsService>();
        builder.Services.AddHttpClient<BE_DelegateWebApplication.Services.ICentralComplaintForwarder, BE_DelegateWebApplication.Services.CentralComplaintForwarder>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<BE_DelegateWebApplication.Services.IWrongSaleExceptionForwarder, BE_DelegateWebApplication.Services.WrongSaleExceptionForwarder>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<BE_DelegateWebApplication.Services.FollowerIdentity.IFollowerIdentityService, BE_DelegateWebApplication.Services.FollowerIdentity.FollowerIdentityService>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.FollowerTracking.IFollowerTrackingRepository, BE_DelegateWebApplication.Services.FollowerTracking.FollowerTrackingRepository>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.FollowerTracking.IFollowerTrackingService, BE_DelegateWebApplication.Services.FollowerTracking.FollowerTrackingService>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.ILegalFollowupFollowerService, BE_DelegateWebApplication.Services.LegalFollowupFollowerService>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.IFollowerFieldFollowupService, BE_DelegateWebApplication.Services.FollowerFieldFollowupService>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.IFollowerSaleAuthService, BE_DelegateWebApplication.Services.FollowerSaleAuthService>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.IDelegateFirstCollectionEvalService, BE_DelegateWebApplication.Services.DelegateFirstCollectionEvalService>();
builder.Services.AddScoped<BE_DelegateWebApplication.Services.IFollowerListStatsService, BE_DelegateWebApplication.Services.FollowerListStatsService>();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();


app.UseCors("free");
app.UseStaticFiles();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();
app.Run();


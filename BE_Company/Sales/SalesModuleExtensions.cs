using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;

namespace BE_Company.Sales
{
    public static class SalesModuleExtensions
    {
        public static IServiceCollection AddSalesManagementModule(this IServiceCollection services)
        {
            services.AddSingleton<SalesDevelopmentGuard>();
            services.AddScoped<SalesIdentityService>();
            services.AddSingleton<ISalesPricingService, SalesPricingService>();
            services.AddScoped<IGlobalCustomerSearchService, DemoGlobalCustomerSearchService>();
            services.AddScoped<CustomerDirectoryService>();
            services.AddScoped<ISalesInventoryService, SalesInventoryService>();
            services.AddScoped<ISalesDraftRepository, SalesDraftRepository>();
            services.AddScoped<ISalesCompleteRepository, SalesCompleteRepository>();
            services.AddScoped<ISalesDocumentService, SalesDocumentService>();
            services.AddScoped<ISalesCompleteService, SalesCompleteService>();
            services.AddScoped<SalesDraftService>();
            services.AddSingleton<IIraqClock, SystemIraqClock>();
            services.AddScoped<ISalesTrackingRepository, SalesTrackingRepository>();
            services.AddScoped<ISalesShiftService, SalesShiftService>();
            services.AddScoped<ISalesLocationIngestService, SalesLocationIngestService>();
            services.AddScoped<ISalesRequestRepository, SalesRequestRepository>();
            services.AddScoped<ISalesRequestService, SalesRequestService>();
            services.AddScoped<ISalesManagerReadRepository, SalesManagerReadRepository>();
            services.AddSingleton(sp =>
            {
                var options = new SalesManagerTrackingOptions();
                sp.GetRequiredService<IConfiguration>().GetSection("SalesManagement:Tracking").Bind(options);
                return options;
            });
            services.AddScoped<SalesManagerQueryService>();
            services.AddSignalR();
            services.AddScoped<ISalesLocationBroadcaster, SignalRSalesLocationBroadcaster>();
            services.AddHostedService<SalesShiftCutoffHostedService>();
            services.AddSingleton<IAuthorizationHandler, SalesRoleHandler>();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(SalesPolicies.AnySales, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesEmployee, SalesRoles.SalesManager)));
                options.AddPolicy(SalesPolicies.SalesEmployee, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesEmployee)));
                options.AddPolicy(SalesPolicies.SalesManager, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
                options.AddPolicy(SalesPolicies.SearchAllBranches, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesEmployee, SalesRoles.SalesManager)));
                options.AddPolicy(SalesPolicies.WriteOwnBranch, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesEmployee, SalesRoles.SalesManager)));
                options.AddPolicy(SalesPolicies.ReadGps, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
                options.AddPolicy(SalesPolicies.ReadOtherSalesEmployees, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
                options.AddPolicy(SalesPolicies.ReadAllBranchSales, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesManager)));
            });
            return services;
        }
    }
}

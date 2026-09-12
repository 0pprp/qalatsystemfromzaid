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
            services.AddScoped<ISalesShopProfileService, SalesShopProfileService>();
            services.AddScoped<ISalesCustomerDocumentService, SalesCustomerDocumentService>();
            services.AddScoped<ISalesCompleteService, SalesCompleteService>();
            services.AddScoped<ISalesPostingService, SalesPostingService>();
            services.AddScoped<ISalesPurchaseRepository, SalesPurchaseRepository>();
            services.AddScoped<ISalesPurchaseService, SalesPurchaseService>();
            services.AddScoped<ISalesExcelCustomerSearchCatalog, SalesExcelCustomerSearchCatalog>();
            services.AddScoped<ISalesExcelCustomerSearchService, SalesExcelCustomerSearchService>();
            services.AddScoped<SalesDraftService>();
            services.AddSingleton<IIraqClock, SystemIraqClock>();
            services.AddScoped<ISalesTrackingRepository, SalesTrackingRepository>();
            services.AddScoped<ISalesShiftService, SalesShiftService>();
            services.AddScoped<ISalesLocationIngestService, SalesLocationIngestService>();
            services.AddScoped<ISalesRequestRepository, SalesRequestRepository>();
            services.AddScoped<ISalesRequestService, SalesRequestService>();
            services.AddScoped<Filtering.ISalesFilterRepository, Filtering.SalesFilterRepository>();
            services.AddScoped<Filtering.ISalesFilterService, Filtering.SalesFilterService>();
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
            services.AddHostedService<SalesPostingHostedService>();
            services.AddScoped<IFollowerDirectoryService, FollowerDirectoryService>();
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
                options.AddPolicy(SalesPolicies.MainAccountant, p =>
                    p.RequireAssertion(ctx =>
                        string.Equals(
                            ctx.User.FindFirst("UserType")?.Value,
                            SalesRoles.UserTypeMainAccountant,
                            StringComparison.Ordinal)));
                options.AddPolicy(SalesPolicies.ReadFollowerGps, p =>
                    p.RequireAssertion(ctx =>
                        string.Equals(
                            ctx.User.FindFirst("UserType")?.Value,
                            SalesRoles.UserTypeMainAccountant,
                            StringComparison.Ordinal)));
                options.AddPolicy(SalesPolicies.SalesFilterEmployee, p =>
                    p.Requirements.Add(new SalesRoleRequirement(SalesRoles.SalesFilterEmployee)));
            });
            return services;
        }
    }
}

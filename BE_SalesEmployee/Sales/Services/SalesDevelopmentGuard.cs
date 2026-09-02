using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.Services
{
    public sealed class SalesDevelopmentGuard
    {
        public const string AllowedDemoDatabase = "DatabaseCompanyNajaf_DEMO";

        private static readonly string[] ProductionCatalogs =
        [
            "DatabaseCompanyNajaf"
        ];

        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public SalesDevelopmentGuard(IConfiguration configuration, IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public bool IsProductionCatalog(string? catalog)
        {
            return !string.IsNullOrWhiteSpace(catalog)
                   && ProductionCatalogs.Any(p => string.Equals(p, catalog, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsAllowedDemo(string? catalog)
        {
            var allowed = _configuration["SalesManagement:AllowedDemoDatabase"] ?? AllowedDemoDatabase;
            return string.Equals(catalog, allowed, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanRunSalesModule(IEnumerable<AdminCity> cities, out string reason)
        {
            var catalogs = cities
                .Select(c => c.Database)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (catalogs.Count == 0)
            {
                reason = "Sales module blocked: LabCities has no Database name. Demo catalog must be DatabaseCompanyNajaf_DEMO.";
                return false;
            }

            if (catalogs.Any(IsProductionCatalog))
            {
                reason = "Sales module blocked: a configured city points at a production catalog. Writes are forbidden.";
                return false;
            }

            var requireDemo = _configuration.GetValue("SalesManagement:RequireDemoDatabase", true);
            if ((_environment.IsDevelopment() || requireDemo) && catalogs.Any(c => !IsAllowedDemo(c)))
            {
                reason = $"Sales module blocked in Development: configured catalog is not {AllowedDemoDatabase}.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}

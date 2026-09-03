using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
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
        private readonly ILogger<SalesDevelopmentGuard> _logger;

        public SalesDevelopmentGuard(
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<SalesDevelopmentGuard> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public bool RequireDemoDatabase =>
            _configuration.GetValue("SalesManagement:RequireDemoDatabase", true);

        public string ConfiguredDemoCatalog =>
            _configuration["SalesManagement:AllowedDemoDatabase"] ?? AllowedDemoDatabase;

        public string? CurrentCatalog
        {
            get
            {
                var cs = GetRawSalesConnectionString();
                if (string.IsNullOrWhiteSpace(cs))
                {
                    return null;
                }
                return new SqlConnectionStringBuilder(cs).InitialCatalog;
            }
        }

        public bool IsProductionCatalog(string? catalog)
        {
            return !string.IsNullOrWhiteSpace(catalog)
                   && ProductionCatalogs.Any(p => string.Equals(p, catalog, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsAllowedDemo(string? catalog)
        {
            return string.Equals(catalog, ConfiguredDemoCatalog, StringComparison.OrdinalIgnoreCase);
        }

        public string? GetSalesConnectionString()
        {
            var cs = GetRawSalesConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
            {
                return null;
            }

            var catalog = new SqlConnectionStringBuilder(cs).InitialCatalog;
            if (RequireDemoDatabase)
            {
                if (IsProductionCatalog(catalog) || !IsAllowedDemo(catalog))
                {
                    return null;
                }
            }

            return cs;
        }

        public bool CanRunSalesModule(out string reason)
        {
            var catalog = CurrentCatalog;
            if (string.IsNullOrWhiteSpace(catalog))
            {
                reason = "Sales module blocked: no database catalog on the sales connection.";
                return false;
            }

            if (RequireDemoDatabase)
            {
                if (IsProductionCatalog(catalog))
                {
                    reason = $"Sales module blocked: catalog '{catalog}' is production. Writes are forbidden.";
                    return false;
                }

                if (!IsAllowedDemo(catalog))
                {
                    reason = $"Sales module blocked: catalog '{catalog}' is not {AllowedDemoDatabase}.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(GetSalesConnectionString()))
            {
                reason = $"Sales module blocked: refusing catalog '{catalog}'.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public async Task<(bool Ok, string Reason)> CanRunSalesModuleAsync(CancellationToken ct = default)
        {
            if (!CanRunSalesModule(out var reason))
            {
                return (false, reason);
            }

            var cs = GetSalesConnectionString();
            try
            {
                await using var connection = new SqlConnection(cs);
                await connection.OpenAsync(ct);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT DB_NAME()";
                var liveName = (await command.ExecuteScalarAsync(ct))?.ToString();
                if (RequireDemoDatabase && !IsAllowedDemo(liveName))
                {
                    return (false, $"Sales module blocked: live catalog '{liveName}' is not {AllowedDemoDatabase}.");
                }

                if (!RequireDemoDatabase && !string.Equals(liveName, CurrentCatalog, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Sales module blocked: live catalog '{liveName}' does not match configured '{CurrentCatalog}'.");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sales database {Catalog} is not reachable.", CurrentCatalog);
                return (false, $"Sales module blocked: cannot connect to '{CurrentCatalog}'.");
            }
        }

        public void LogDevelopmentCatalog()
        {
            _logger.LogInformation(
                "Sales DB: {Catalog} RequireDemo={RequireDemo} Environment={Env}",
                CurrentCatalog,
                RequireDemoDatabase,
                _environment.EnvironmentName);
        }

        private string? GetRawSalesConnectionString()
        {
            if (RequireDemoDatabase)
            {
                var sales = _configuration.GetConnectionString("SalesDemoConnection");
                if (!string.IsNullOrWhiteSpace(sales))
                {
                    return sales;
                }
            }

            return _configuration.GetConnectionString("DataBaseConnection");
        }
    }
}

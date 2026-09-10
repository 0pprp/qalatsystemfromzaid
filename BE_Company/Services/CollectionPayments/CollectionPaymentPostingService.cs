using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Services.CollectionPayments
{
    public interface ICollectionPaymentPostingService
    {
        Task<int> PostEligibleAsync(CancellationToken ct);
    }

    public sealed class CollectionPaymentPostingService : ICollectionPaymentPostingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CollectionPaymentPostingService> _logger;

        /// <summary>System user used for automated box posting (falls back to 1).</summary>
        private const int AutoPostUserIdFallback = 1;

        public CollectionPaymentPostingService(
            IConfiguration configuration,
            ILogger<CollectionPaymentPostingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<int> PostEligibleAsync(CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("DataBaseConnection");
            if (string.IsNullOrWhiteSpace(cs))
            {
                return 0;
            }

            var userId = _configuration.GetValue("CollectionPayments:AutoPostUserId", AutoPostUserIdFallback);

            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);

            var p = new DynamicParameters();
            p.Add("@UserCreateID", userId);
            p.Add("@PostedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            try
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "CustomersPaymentsRequest_PostEligible",
                        p,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 600,
                        cancellationToken: ct));

                var posted = p.Get<int>("@PostedCount");
                if (posted > 0)
                {
                    _logger.LogInformation("Collection payments auto-posted: {Count}", posted);
                }

                return posted;
            }
            catch (SqlException ex) when (ex.Message.Contains("CustomersPaymentsRequest_PostEligible", StringComparison.OrdinalIgnoreCase)
                                          || ex.Number == 2812)
            {
                // SP not deployed yet — skip quietly until migration runs.
                _logger.LogWarning("CustomersPaymentsRequest_PostEligible not available yet.");
                return 0;
            }
        }
    }

    public sealed class CollectionPaymentPostingHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<CollectionPaymentPostingHostedService> _logger;

        public CollectionPaymentPostingHostedService(
            IServiceScopeFactory scopes,
            ILogger<CollectionPaymentPostingHostedService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Catch-up sweep: do not rely on firing exactly at 16:00:00.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var posting = scope.ServiceProvider.GetRequiredService<ICollectionPaymentPostingService>();
                    await posting.PostEligibleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Collection payment posting sweep skipped.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}

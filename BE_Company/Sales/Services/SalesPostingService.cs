namespace BE_Company.Sales.Services
{
    public interface ISalesPostingService
    {
        Task PostSaleAsync(int saleId, CancellationToken ct);
        Task<int> PostDueSalesAsync(CancellationToken ct);
    }

    public sealed class SalesPostingService : ISalesPostingService
    {
        private readonly ISalesCompleteRepository _complete;
        private readonly IIraqClock _clock;
        private readonly ILogger<SalesPostingService> _logger;

        public SalesPostingService(
            ISalesCompleteRepository complete,
            IIraqClock clock,
            ILogger<SalesPostingService> logger)
        {
            _complete = complete;
            _clock = clock;
            _logger = logger;
        }

        public Task PostSaleAsync(int saleId, CancellationToken ct) =>
            _complete.PostToMainSystemAsync(saleId, ct);

        public async Task<int> PostDueSalesAsync(CancellationToken ct)
        {
            if (!SalesPostingRules.IsPostingWindow(_clock))
            {
                return 0;
            }

            var ids = await _complete.ListUnpostedCompletedSaleIdsAsync(ct);
            var posted = 0;
            foreach (var saleId in ids)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await _complete.PostToMainSystemAsync(saleId, ct);
                    posted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sales posting failed for SaleId {SaleId}.", saleId);
                }
            }

            return posted;
        }
    }

    public sealed class SalesPostingHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<SalesPostingHostedService> _logger;

        public SalesPostingHostedService(IServiceScopeFactory scopes, ILogger<SalesPostingHostedService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var guard = scope.ServiceProvider.GetRequiredService<SalesDevelopmentGuard>();
                    var check = await guard.CanRunSalesModuleAsync(stoppingToken);
                    if (check.Ok)
                    {
                        var posting = scope.ServiceProvider.GetRequiredService<ISalesPostingService>();
                        await posting.PostDueSalesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sales posting sweep skipped.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}

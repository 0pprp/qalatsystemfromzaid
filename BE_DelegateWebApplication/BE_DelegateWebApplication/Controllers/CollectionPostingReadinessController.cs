using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Controllers;

/// <summary>
/// Financial posting readiness — API up ≠ collection catch-up procedure available.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class CollectionPostingReadinessController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CollectionPostingReadinessController> _logger;

    public CollectionPostingReadinessController(
        IConfiguration configuration,
        ILogger<CollectionPostingReadinessController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var cs = _configuration.GetConnectionString("DataBaseConnection");
        if (string.IsNullOrWhiteSpace(cs))
        {
            return StatusCode(503, new
            {
                ready = false,
                reason = "missing_connection_string",
                apiAlive = true,
                postingProcedureExists = false,
            });
        }

        try
        {
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);

            var procExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
SELECT CASE WHEN OBJECT_ID(N'dbo.CustomersPaymentsRequest_PostEligible', N'P') IS NULL THEN 0 ELSE 1 END
""", cancellationToken: ct)) == 1;

            var queueAgeMinutes = 0.0;
            var pendingEligible = 0;
            var stuckCount = 0;
            var tableExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
SELECT CASE WHEN OBJECT_ID(N'dbo.CustomersPaymentsRequest', N'U') IS NULL THEN 0 ELSE 1 END
""", cancellationToken: ct)) == 1;

            if (tableExists)
            {
                pendingEligible = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
SELECT COUNT(1) FROM dbo.CustomersPaymentsRequest
WHERE EligibleForPostingAtUtc IS NOT NULL
  AND EligibleForPostingAtUtc <= SYSUTCDATETIME()
  AND NOT EXISTS (
        SELECT 1 FROM dbo.CustomersPayments cp
        WHERE cp.ClientPaymentId = CustomersPaymentsRequest.ClientPaymentId)
""", cancellationToken: ct));

                queueAgeMinutes = await connection.ExecuteScalarAsync<double?>(new CommandDefinition("""
SELECT TOP 1 DATEDIFF(MINUTE, ReceivedAtUtc, SYSUTCDATETIME())
FROM dbo.CustomersPaymentsRequest
WHERE EligibleForPostingAtUtc IS NOT NULL
  AND EligibleForPostingAtUtc <= SYSUTCDATETIME()
ORDER BY ReceivedAtUtc ASC
""", cancellationToken: ct)) ?? 0;

                stuckCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
SELECT COUNT(1) FROM dbo.CustomersPaymentsRequest
WHERE EligibleForPostingAtUtc IS NOT NULL
  AND EligibleForPostingAtUtc <= DATEADD(HOUR, -6, SYSUTCDATETIME())
  AND NOT EXISTS (
        SELECT 1 FROM dbo.CustomersPayments cp
        WHERE cp.ClientPaymentId = CustomersPaymentsRequest.ClientPaymentId)
""", cancellationToken: ct));
            }

            var ready = procExists && tableExists && stuckCount == 0;
            if (!ready)
            {
                _logger.LogWarning(
                    "Collection posting not ready. proc={Proc} pending={Pending} stuck={Stuck} ageMin={Age}",
                    procExists, pendingEligible, stuckCount, queueAgeMinutes);
            }

            return ready
                ? Ok(new
                {
                    ready = true,
                    apiAlive = true,
                    postingProcedureExists = procExists,
                    requestTableExists = tableExists,
                    pendingEligible,
                    stuckOlderThan6h = stuckCount,
                    oldestEligibleAgeMinutes = queueAgeMinutes,
                })
                : StatusCode(503, new
                {
                    ready = false,
                    apiAlive = true,
                    postingProcedureExists = procExists,
                    requestTableExists = tableExists,
                    pendingEligible,
                    stuckOlderThan6h = stuckCount,
                    oldestEligibleAgeMinutes = queueAgeMinutes,
                    reason = !procExists
                        ? "missing_post_eligible_procedure"
                        : !tableExists
                            ? "missing_request_table"
                            : "stuck_queue",
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collection readiness check failed");
            return StatusCode(503, new
            {
                ready = false,
                apiAlive = true,
                reason = "database_unreachable",
            });
        }
    }
}

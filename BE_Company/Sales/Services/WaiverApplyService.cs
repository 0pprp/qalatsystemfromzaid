using System.Globalization;
using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Models;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class WaiverApplyRequest
    {
        public Guid? WaiverId { get; init; }
        public int CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public double Amount { get; init; }
        public string? Note { get; init; }
        public string? DecidedByUserName { get; init; }
        public string? DecidedByDisplayName { get; init; }
    }

    public interface IWaiverApplyService
    {
        Task ApplyAsync(SalesIdentity actor, WaiverApplyRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Posts waiver amount as a customer payment and creates an unread accountant notification
    /// via CustomerWeekDecisions (DecisionType = تنازل) + DecisionNotifications.
    /// Uses the same branch financial connection as the sales module (DataBaseConnection /
    /// SalesDemoConnection via SalesDevelopmentGuard). WaiverId is a durable idempotency key.
    /// </summary>
    public sealed class WaiverApplyService : IWaiverApplyService
    {
        public const string DecisionType = "تنازل";

        private readonly SalesDevelopmentGuard _guard;
        private readonly ICustomersPaymentsRepository _payments;

        public WaiverApplyService(SalesDevelopmentGuard guard, ICustomersPaymentsRepository payments)
        {
            _guard = guard;
            _payments = payments;
        }

        public async Task ApplyAsync(SalesIdentity actor, WaiverApplyRequest request, CancellationToken ct = default)
        {
            if (!actor.IsGateway && !SalesRoles.IsSalesManager(actor.UserType))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }

            if (!WaiverApplyRules.IsValidWaiverId(request.WaiverId))
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    "معرّف التنازل (WaiverId) مطلوب للتطبيق الآمن.");
            }

            if (request.CustomerId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "معرّف الزبون مطلوب");
            }

            if (request.Amount <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "مبلغ التنازل غير صالح");
            }

            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(WaiverApplyRules.ClaimSql, cancellationToken: ct));

            var already = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                WaiverApplyRules.ExistsSql,
                new { WaiverId = request.WaiverId!.Value },
                cancellationToken: ct));
            if (already == 1)
            {
                // Durable no-op: payment/decision already posted for this WaiverId.
                return;
            }

            var customerName = (request.CustomerName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(customerName))
            {
                customerName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                    "SELECT ISNULL(CustomerName, N'') FROM dbo.Customers WHERE CustomerID=@CustomerId",
                    new { request.CustomerId }, cancellationToken: ct)) ?? "";
            }

            var amountText = request.Amount.ToString("#,##0.##", CultureInfo.InvariantCulture);
            var note = string.IsNullOrWhiteSpace(request.Note)
                ? $"تم التنازل عن المبلغ {amountText} من حساب الزبون {customerName}"
                : request.Note.Trim();

            var userId = actor.IsGateway || actor.EmployeeId <= 0 ? (int?)null : actor.EmployeeId;

            await using var tx = await connection.BeginTransactionAsync(ct);
            try
            {
                try
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        WaiverApplyRules.TryClaimSql,
                        new
                        {
                            WaiverId = request.WaiverId.Value,
                            request.CustomerId,
                            request.Amount,
                            ActorEmployeeId = userId
                        },
                        tx,
                        cancellationToken: ct));
                }
                catch (Exception ex) when (WaiverApplyRules.IsUniqueKeyViolation(ex))
                {
                    await tx.RollbackAsync(ct);
                    return;
                }

                var payment = await _payments.CustomersPayments_Create(
                    new CustomersPaymentsPostDTO
                    {
                        CustomerID = request.CustomerId,
                        PaymentAmount = request.Amount,
                        PaymentDate = DateTime.Now,
                        UserCreateID = userId
                    },
                    connection,
                    tx,
                    ct);

                if (payment?.CustomerPaymentID is not > 0)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status502BadGateway,
                        "تعذر تسجيل تسديد التنازل");
                }

                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Saturday);
                if (weekStart > today) weekStart = weekStart.AddDays(-7);
                var weekEnd = weekStart.AddDays(6);

                var amountTotal = await connection.ExecuteScalarAsync<double?>(new CommandDefinition("""
SELECT ISNULL(AmountTotalSales, 0) FROM dbo.Customers WHERE CustomerID = @CustomerId;
""", new { request.CustomerId }, tx, cancellationToken: ct)) ?? 0;
                var weekPaid = request.Amount;
                var paidPercent = amountTotal > 0 ? Math.Round(weekPaid * 100.0 / amountTotal, 2) : 0;

                var decisionId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.CustomerWeekDecisions
    (CustomerID, UserID, DecisionType, WeekPaid, AmountTotalSales, PaidPercent,
     WeekStartDate, WeekEndDate, SnoozeUntil, Note, CreatedDate)
VALUES
    (@CustomerId, @UserId, @DecisionType, @WeekPaid, @AmountTotal, @PaidPercent,
     @WeekStart, @WeekEnd, NULL, @Note, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);
""", new
                {
                    request.CustomerId,
                    UserId = userId,
                    DecisionType,
                    WeekPaid = weekPaid,
                    AmountTotal = amountTotal,
                    PaidPercent = paidPercent,
                    WeekStart = weekStart,
                    WeekEnd = weekEnd,
                    Note = note
                }, tx, cancellationToken: ct));

                await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.DecisionNotifications (DecisionID, IsRead, CreatedDate)
VALUES (@DecisionId, 0, GETDATE());
""", new { DecisionId = decisionId }, tx, cancellationToken: ct));

                await connection.ExecuteAsync(new CommandDefinition(
                    WaiverApplyRules.CompleteClaimSql,
                    new
                    {
                        WaiverId = request.WaiverId.Value,
                        CustomerPaymentID = payment.CustomerPaymentID!.Value,
                        DecisionId = decisionId
                    },
                    tx,
                    cancellationToken: ct));

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}

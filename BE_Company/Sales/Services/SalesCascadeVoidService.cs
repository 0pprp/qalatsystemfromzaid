using System.Data;
using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Compensating transactions when a manager soft-deletes a request linked to a sale:
    /// optional payment delete → restore stock → remove official sale → void draft.
    /// All financial deletes for a void run in one DB transaction.
    /// </summary>
    public sealed class SalesCascadeVoidService : ISalesCascadeVoidService
    {
        public const string PaymentsConfirmRequiredCode = "payments_confirm_required";
        public const string DraftVoidedStatus = "Voided";

        private readonly SalesDevelopmentGuard _guard;
        private readonly ICustomersPaymentsRepository _payments;
        private readonly IIraqClock _clock;

        public SalesCascadeVoidService(
            SalesDevelopmentGuard guard,
            ICustomersPaymentsRepository payments,
            IIraqClock clock)
        {
            _guard = guard;
            _payments = payments;
            _clock = clock;
        }

        public async Task<SalesRequestSoftDeleteImpactDTO> GetImpactAsync(SalesRequestDTO request, CancellationToken ct)
        {
            var impact = BaseImpact(request);
            if (request.ConvertedToSaleId is not > 0)
            {
                impact.WarningAr = "سيتم نقل الطلب إلى تبويب المحذوفين مع الاحتفاظ بكل البيانات.";
                return impact;
            }

            await using var connection = Open();
            await connection.OpenAsync(ct);
            var draft = await LoadDraftAsync(connection, request.ConvertedToSaleId.Value, ct);
            if (draft == null)
            {
                impact.HasLinkedDraft = false;
                impact.WarningAr =
                    "الطلب مرتبط بمعرف مبيع لكن المسودة غير موجودة. سيتم الأرشفة فقط دون لمس المبيعات/المخزن.";
                return impact;
            }

            ApplyDraft(impact, draft);
            if (!impact.IsPosted)
            {
                impact.WillDeleteOfficialSale = false;
                impact.WillRestoreInventory = false;
                impact.WarningAr = SalesCompleteRules.AlreadyCompleted(draft.Status)
                    ? "سيتم إبطال المسودة المكتملة غير المرحلة (تحرير الحجز) وأرشفة الطلب. لا خصم مخزني رسمي بعد."
                    : "سيتم إبطال المسودة المرتبطة وأرشفة الطلب.";
                return impact;
            }

            var customerSaleId = await ResolveProvenCustomerSaleIdAsync(connection, draft, ct);
            impact.CustomerSaleId = customerSaleId;
            impact.WillDeleteOfficialSale = customerSaleId is > 0;
            impact.WillRestoreInventory = customerSaleId is > 0;

            if (customerSaleId is not > 0 || !SalesCascadeVoidRules.ShouldDeleteOfficialSale(customerSaleId))
            {
                impact.WillDeleteOfficialSale = false;
                impact.WillRestoreInventory = false;
                impact.WarningAr = draft.CustomerSaleId is > 0
                    ? SalesCascadeVoidRules.MissingOfficialSaleWarningAr
                    : SalesCascadeVoidRules.UnprovenSaleLinkWarningAr;
                return impact;
            }

            var (count, total) = await LoadPaymentStatsAsync(connection, draft.CustomerId, ct);
            impact.PaymentCount = count;
            impact.PaymentTotal = total;
            var saleCount = await CountOfficialSalesAsync(connection, draft.CustomerId, ct);
            var canDeletePayments = SalesCascadeVoidRules.CanDeleteCustomerPaymentsForSale(customerSaleId, saleCount);
            impact.RequiresPaymentConfirm = count > 0 && canDeletePayments;
            if (count > 0 && !canDeletePayments)
            {
                impact.RequiresPaymentConfirm = false;
                impact.WillDeleteOfficialSale = false;
                impact.WillRestoreInventory = false;
                impact.WarningAr = SalesCascadeVoidRules.AmbiguousPaymentsMessageAr;
                return impact;
            }

            impact.WarningAr = count > 0
                ? $"هذا المبيع مرتبط بـ {count} تسديد(ات) بمبلغ {total:N0}. بعد التأكيد ستُحذف التسديدات والمبيع وتُرجع الكميات للمخزن."
                : "سيُحذف المبيع من المبيعات وتُرجع الكميات للمخزن، ثم يُنقل الطلب إلى المحذوفين.";
            return impact;
        }

        public async Task VoidLinkedAsync(
            SalesIdentity manager,
            SalesRequestDTO request,
            bool confirmCascadePayments,
            string reason,
            CancellationToken ct)
        {
            if (request.ConvertedToSaleId is not > 0)
            {
                return;
            }

            await using var connection = Open();
            await connection.OpenAsync(ct);
            var draft = await LoadDraftAsync(connection, request.ConvertedToSaleId.Value, ct);
            if (draft == null)
            {
                return;
            }

            if (SalesPostingStatuses.IsPosted(draft.PostingStatus))
            {
                var customerSaleId = await ResolveProvenCustomerSaleIdAsync(connection, draft, ct);
                if (!SalesCascadeVoidRules.ShouldDeleteOfficialSale(customerSaleId))
                {
                    // Official sale already gone / never linked — still void the draft so soft-delete can finish.
                    await VoidDraftAsync(connection, draft.SaleId, ct);
                    return;
                }

                var (paymentCount, _) = await LoadPaymentStatsAsync(connection, draft.CustomerId, ct);
                var saleCount = await CountOfficialSalesAsync(connection, draft.CustomerId, ct);
                var canDeletePayments = SalesCascadeVoidRules.CanDeleteCustomerPaymentsForSale(
                    customerSaleId, saleCount);

                if (paymentCount > 0 && !canDeletePayments)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status409Conflict,
                        SalesCascadeVoidRules.AmbiguousPaymentsMessageAr,
                        SalesCascadeVoidRules.AmbiguousPaymentsCode);
                }

                if (paymentCount > 0 && !confirmCascadePayments)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status409Conflict,
                        $"هذا المبيع مرتبط بـ {paymentCount} تسديد(ات). أكّد الحذف لحذف التسديدات والمبيع وإرجاع المخزن.",
                        PaymentsConfirmRequiredCode);
                }

                await DeleteOfficialSaleRestoreAndPaymentsAsync(
                    connection,
                    customerSaleId!.Value,
                    draft.CustomerId,
                    manager.EmployeeId,
                    request.Id,
                    reason,
                    deletePayments: paymentCount > 0 && canDeletePayments,
                    ct);
            }

            await VoidDraftAsync(connection, draft.SaleId, ct);
        }

        private static SalesRequestSoftDeleteImpactDTO BaseImpact(SalesRequestDTO request) => new()
        {
            RequestId = request.Id,
            LinkedDraftSaleId = request.ConvertedToSaleId,
            HasLinkedDraft = request.ConvertedToSaleId is > 0
        };

        private static void ApplyDraft(SalesRequestSoftDeleteImpactDTO impact, DraftHeader draft)
        {
            impact.HasLinkedDraft = true;
            impact.LinkedDraftSaleId = draft.SaleId;
            impact.DraftStatus = draft.Status;
            impact.PostingStatus = draft.PostingStatus;
            impact.IsPosted = SalesPostingStatuses.IsPosted(draft.PostingStatus);
            impact.CustomerId = draft.CustomerId;
            impact.CustomerSaleId = draft.CustomerSaleId;
        }

        private SqlConnection Open()
        {
            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
            return new SqlConnection(cs);
        }

        private static async Task<DraftHeader?> LoadDraftAsync(SqlConnection connection, int saleId, CancellationToken ct)
        {
            await EnsureCustomerSaleIdColumnAsync(connection, ct);
            return await connection.QueryFirstOrDefaultAsync<DraftHeader>(new CommandDefinition(
                @"SELECT SaleId, EmployeeId, Status, CustomerId, CustomerSaleId, Phone,
                         ISNULL(PostingStatus, N'Pending') AS PostingStatus, PostedAtUtc
                  FROM dbo.SalesDrafts
                  WHERE SaleId = @SaleId",
                new { SaleId = saleId },
                cancellationToken: ct));
        }

        private static async Task EnsureCustomerSaleIdColumnAsync(SqlConnection connection, CancellationToken ct)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SalesDraftSchema.AddCustomerSaleIdSql,
                cancellationToken: ct));
        }

        /// <summary>
        /// Only returns draft.CustomerSaleId when that exact official row still exists.
        /// Never infers "the customer's only sale" or phone-based matches for destructive void.
        /// </summary>
        private static async Task<int?> ResolveProvenCustomerSaleIdAsync(
            SqlConnection connection,
            DraftHeader draft,
            CancellationToken ct)
        {
            if (draft.CustomerSaleId is not > 0)
            {
                return SalesCascadeVoidRules.ResolveProvenOfficialSaleId(null, storedIdExistsInOfficial: false);
            }

            var exists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                @"SELECT TOP 1 CustomerSaleID FROM dbo.CustomersSales WHERE CustomerSaleID = @Id",
                new { Id = draft.CustomerSaleId },
                cancellationToken: ct));
            var storedExists = exists is > 0;
            if (SalesCascadeVoidRules.ShouldStopAfterPhantomStoredId(draft.CustomerSaleId, storedExists))
            {
                return null;
            }

            return SalesCascadeVoidRules.ResolveProvenOfficialSaleId(draft.CustomerSaleId, storedExists);
        }

        private static async Task<int> CountOfficialSalesAsync(
            SqlConnection connection,
            int? customerId,
            CancellationToken ct)
        {
            if (customerId is not > 0)
            {
                return 0;
            }

            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1) FROM dbo.CustomersSales WHERE CustomerID = @CustomerId",
                new { CustomerId = customerId },
                cancellationToken: ct));
        }

        private static async Task<(int Count, decimal Total)> LoadPaymentStatsAsync(
            SqlConnection connection,
            int? customerId,
            CancellationToken ct)
        {
            if (customerId is not > 0)
            {
                return (0, 0);
            }

            try
            {
                var row = await connection.QueryFirstOrDefaultAsync<(int Count, decimal Total)>(new CommandDefinition(
                    @"SELECT COUNT(1) AS Count,
                             CAST(ISNULL(SUM(ISNULL(AmountDenar, 0)), 0) AS decimal(18,0)) AS Total
                      FROM dbo.View_CustomersPayments
                      WHERE CustomerID = @CustomerId",
                    new { CustomerId = customerId },
                    cancellationToken: ct));
                return (row.Count, row.Total);
            }
            catch
            {
                var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    @"SELECT COUNT(1) FROM dbo.CustomersPayments WHERE CustomerID = @CustomerId",
                    new { CustomerId = customerId },
                    cancellationToken: ct));
                return (count, 0);
            }
        }

        private async Task DeleteOfficialSaleRestoreAndPaymentsAsync(
            SqlConnection connection,
            int customerSaleId,
            int? customerId,
            int userId,
            int requestId,
            string reason,
            bool deletePayments,
            CancellationToken ct)
        {
            await using var tx = await connection.BeginTransactionAsync(ct);
            try
            {
                if (deletePayments && customerId is > 0)
                {
                    var ids = (await connection.QueryAsync<int>(new CommandDefinition(
                        @"SELECT CustomerPaymentID FROM dbo.CustomersPayments WHERE CustomerID = @CustomerId",
                        new { CustomerId = customerId },
                        tx,
                        cancellationToken: ct))).ToList();

                    foreach (var paymentId in ids)
                    {
                        await _payments.CustomersPayments_Delete(
                            paymentId, userId, connection, (SqlTransaction)tx, ct);
                    }

                    if (await TableExistsAsync(connection, (SqlTransaction)tx, "CustomersPaymentsRequest", ct))
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            @"DELETE FROM dbo.CustomersPaymentsRequest WHERE CustomerID = @CustomerId",
                            new { CustomerId = customerId },
                            tx,
                            cancellationToken: ct));
                    }
                }

                if (await ProcedureExistsAsync(connection, (SqlTransaction)tx, "DeleteSelectItemsSalesAsyncID", ct))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "DeleteSelectItemsSalesAsyncID",
                        new { CustomerSaleID = customerSaleId },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }

                if (await ProcedureExistsAsync(connection, (SqlTransaction)tx, "DeleteCustomersSalesAsyncID", ct))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "DeleteCustomersSalesAsyncID",
                        new { CustomerSaleID = customerSaleId },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }

                if (await ProcedureExistsAsync(connection, (SqlTransaction)tx, "RestoreItemQuantitySale", ct))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "RestoreItemQuantitySale",
                        new { CustomerSaleID = customerSaleId },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }
                else
                {
                    await RestoreQuantitiesFallbackAsync(connection, (SqlTransaction)tx, customerSaleId, ct);
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    @"DELETE FROM dbo.SelectItemsSales WHERE CustomerSaleID = @CustomerSaleID",
                    new { CustomerSaleID = customerSaleId },
                    tx,
                    cancellationToken: ct));

                await connection.ExecuteAsync(new CommandDefinition(
                    @"DELETE FROM dbo.CustomersSales WHERE CustomerSaleID = @CustomerSaleID",
                    new { CustomerSaleID = customerSaleId },
                    tx,
                    cancellationToken: ct));

                if (customerId is > 0)
                {
                    var remaining = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                        @"SELECT COUNT(1) FROM dbo.CustomersSales WHERE CustomerID = @CustomerId",
                        new { CustomerId = customerId },
                        tx,
                        cancellationToken: ct));
                    if (remaining == 0 && await ColumnExistsAsync(connection, (SqlTransaction)tx, "Customers", "CustomerState", ct))
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            @"UPDATE dbo.Customers SET CustomerState = 0 WHERE CustomerID = @CustomerId",
                            new { CustomerId = customerId },
                            tx,
                            cancellationToken: ct));
                    }
                }

                if (await TableExistsAsync(connection, (SqlTransaction)tx, "Activities", ct))
                {
                    var detail = Trunc(
                        $"تم حذف المبيع #{customerSaleId} بسبب أرشفة طلب البيع #{requestId}: {reason}",
                        400);
                    await connection.ExecuteAsync(new CommandDefinition(
                        @"INSERT INTO dbo.Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
                          VALUES (@UserID, @Desc, @At, 'false', NEWID())",
                        new
                        {
                            UserID = userId,
                            Desc = detail,
                            At = _clock.UtcNow
                        },
                        tx,
                        cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        private static async Task RestoreQuantitiesFallbackAsync(
            SqlConnection connection,
            SqlTransaction tx,
            int customerSaleId,
            CancellationToken ct)
        {
            var lines = await connection.QueryAsync<(int ItemId, int Qty)>(new CommandDefinition(
                @"SELECT ItemID AS ItemId, Quantity AS Qty
                  FROM dbo.SelectItemsSales
                  WHERE CustomerSaleID = @CustomerSaleID",
                new { CustomerSaleID = customerSaleId },
                tx,
                cancellationToken: ct));
            foreach (var line in lines)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"UPDATE dbo.Items SET Quantity = ISNULL(Quantity, 0) + @Qty WHERE ItemID = @ItemId",
                    new { line.ItemId, line.Qty },
                    tx,
                    cancellationToken: ct));
            }
        }

        private static async Task VoidDraftAsync(SqlConnection connection, int saleId, CancellationToken ct)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.SalesDrafts
                  SET Status = @Voided,
                      PostingStatus = @Voided,
                      LastPostingError = N'Voided by manager soft-delete'
                  WHERE SaleId = @SaleId",
                new { SaleId = saleId, Voided = DraftVoidedStatus },
                cancellationToken: ct));
        }

        private static async Task<bool> ProcedureExistsAsync(
            SqlConnection connection,
            SqlTransaction? tx,
            string name,
            CancellationToken ct) =>
            await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT CASE WHEN OBJECT_ID(@Name, N'P') IS NULL THEN 0 ELSE 1 END",
                new { Name = "dbo." + name },
                tx,
                cancellationToken: ct)) == 1;

        private static async Task<bool> TableExistsAsync(
            SqlConnection connection,
            SqlTransaction? tx,
            string table,
            CancellationToken ct) =>
            await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT CASE WHEN OBJECT_ID(@Name, N'U') IS NULL THEN 0 ELSE 1 END",
                new { Name = "dbo." + table },
                tx,
                cancellationToken: ct)) == 1;

        private static async Task<bool> ColumnExistsAsync(
            SqlConnection connection,
            SqlTransaction tx,
            string table,
            string column,
            CancellationToken ct) =>
            await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT CASE WHEN COL_LENGTH(@Table, @Column) IS NULL THEN 0 ELSE 1 END",
                new { Table = "dbo." + table, Column = column },
                tx,
                cancellationToken: ct)) == 1;

        private static string Trunc(string value, int max) =>
            value.Length <= max ? value : value[..max];

        private sealed class DraftHeader
        {
            public int SaleId { get; set; }
            public int EmployeeId { get; set; }
            public string Status { get; set; } = "";
            public int? CustomerId { get; set; }
            public int? CustomerSaleId { get; set; }
            public string? Phone { get; set; }
            public string? PostingStatus { get; set; }
            public DateTime? PostedAtUtc { get; set; }
        }
    }
}

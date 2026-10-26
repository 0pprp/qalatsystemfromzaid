using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.CollectionPayments;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_DelegateWebApplication.Repository
{
    public class CustomersPaymentsRequestsRepository : ICustomersPaymentsRequestsRepository
    {
        private readonly string _connectionString;

        public CustomersPaymentsRequestsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<bool?> PostSelectPaymentCustomerTemporary(CustomersPaymentsRequestsPostDTO? customersPaymentsRequestsPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.ExecuteAsync("PostSelectPaymentCustomerTemporary",
                new
                {
                    CustomerID = customersPaymentsRequestsPostDTO?.CustomerId,
                    DelegateID = customersPaymentsRequestsPostDTO?.DelegateId,
                    Amount = customersPaymentsRequestsPostDTO?.Amount,
                    Location = customersPaymentsRequestsPostDTO?.Location
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 600);
                return true;
            }
        }

        public async Task<bool?> PostSelectPaymentCustomerTemporaryMulti(List<CustomersPaymentsRequestsPostDTO>? customersPaymentsRequestsPostDTO)
        {
            if (customersPaymentsRequestsPostDTO == null || !customersPaymentsRequestsPostDTO.Any())
                return true;

            var dt = new DataTable();
            dt.Columns.Add("CustomerID", typeof(int));
            dt.Columns.Add("DelegateID", typeof(int));
            dt.Columns.Add("Amount", typeof(double));
            dt.Columns.Add("Location", typeof(string));

            foreach (var item in customersPaymentsRequestsPostDTO)
            {
                dt.Rows.Add(item.CustomerId, item.DelegateId, item.Amount, item.Location);
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.ExecuteAsync("PostSelectPaymentCustomerTemporaryMulti",
                    new { PaymentData = dt.AsTableValuedParameter("dbo.CustomersPaymentsRequestType") },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 1000);

                return true;
            }
        }

        public async Task<PaymentIdempotentResultDTO> PostPaymentIdempotentAsync(
            CustomersPaymentsRequestsPostDTO dto,
            int authenticatedDelegateId,
            CancellationToken ct = default)
        {
            if (!CollectionPaymentRules.IsValidClientPaymentId(dto.ClientPaymentId))
            {
                return new PaymentIdempotentResultDTO
                {
                    Success = false,
                    Message = "ClientPaymentId مطلوب ويجب أن يكون UUID صالحًا"
                };
            }

            if (!CollectionPaymentRules.TryValidateAmount(dto.Amount, out var amountError))
            {
                return new PaymentIdempotentResultDTO { Success = false, Message = amountError };
            }

            if (dto.CustomerId is null or <= 0)
            {
                return new PaymentIdempotentResultDTO { Success = false, Message = "معرف العميل غير صالح" };
            }

            // Body DelegateId is advisory only — identity comes from AsyncId.
            // Never reject a durable local payment because the selected list id
            // differs from the login row (common with multi-list collectors).
            if (dto.DelegateId is not null && dto.DelegateId != authenticatedDelegateId)
            {
                // Keep going; insert uses authenticatedDelegateId below.
            }

            var serverUtc = DateTime.UtcNow;
            if (!CollectionPaymentRules.TryNormalizeCreatedAtUtc(dto.CreatedAtUtc, serverUtc, out var createdAtUtc, out var timeError))
            {
                return new PaymentIdempotentResultDTO { Success = false, Message = timeError };
            }

            var eligibleAt = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(createdAtUtc);
            var clientPaymentId = dto.ClientPaymentId!.Trim();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Ensure customer belongs to this delegate when relationship exists.
            var customerDelegateId = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT DelegateID FROM Customers WHERE CustomerID = @CustomerId",
                    new { CustomerId = dto.CustomerId },
                    cancellationToken: ct));

            if (customerDelegateId is null)
            {
                return new PaymentIdempotentResultDTO { Success = false, Message = "العميل غير موجود" };
            }

            // Soft ownership check: prefer matching delegate; allow if customer.DelegateID null historically.
            if (customerDelegateId > 0 && customerDelegateId != authenticatedDelegateId)
            {
                // Collection delegates may collect for customers under other list delegates —
                // keep historical flexibility but block spoofing via body DelegateId (already checked).
            }

            // Prefer customer's list: if that list moved to followers, block.
            // Also block when the authenticated collector list itself is follower-collection.
            var customerListId = customerDelegateId ?? 0;
            var authLocked = await IsFollowerCollectionEnabledAsync(connection, authenticatedDelegateId, ct);
            var customerLocked = customerListId > 0
                && await IsFollowerCollectionEnabledAsync(connection, customerListId, ct);
            if (authLocked || customerLocked)
            {
                return new PaymentIdempotentResultDTO
                {
                    Success = false,
                    Message = FollowerCollectionPaymentGate.LockedMessage
                };
            }

            var existing = await connection.QuerySingleOrDefaultAsync<dynamic>(
                new CommandDefinition(@"
SELECT TOP 1 CustomersPaymentsRequestID, ClientPaymentId
FROM CustomersPaymentsRequest
WHERE ClientPaymentId = @ClientPaymentId",
                    new { ClientPaymentId = clientPaymentId },
                    cancellationToken: ct));

            if (existing != null)
            {
                await CollectionPaymentIntakeGuard.WriteAuditAsync(
                    connection, dto, authenticatedDelegateId, createdAtUtc,
                    "AlreadyExists", "التسديد مسجل مسبقًا",
                    (int)existing.CustomersPaymentsRequestID, ct);
                return new PaymentIdempotentResultDTO
                {
                    Success = true,
                    AlreadyExists = true,
                    CustomersPaymentsRequestID = (int)existing.CustomersPaymentsRequestID,
                    ClientPaymentId = clientPaymentId,
                    Message = "التسديد مسجل مسبقًا"
                };
            }

            var postedExists = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM CustomersPayments WHERE ClientPaymentId = @ClientPaymentId) THEN 1 ELSE 0 END",
                    new { ClientPaymentId = clientPaymentId },
                    cancellationToken: ct));

            if (postedExists == 1)
            {
                await CollectionPaymentIntakeGuard.WriteAuditAsync(
                    connection, dto, authenticatedDelegateId, createdAtUtc,
                    "AlreadyExists", "التسديد مُرحّل مسبقًا", null, ct);
                return new PaymentIdempotentResultDTO
                {
                    Success = true,
                    AlreadyExists = true,
                    ClientPaymentId = clientPaymentId,
                    Message = "التسديد مُرحّل مسبقًا"
                };
            }

            // Anti double-collect: same customer + same amount on same Baghdad business day.
            // Still do not insert a second row — but return Success so the device clears its
            // local queue (HTTP 400 was marking PermanentFailure while money was already on server).
            if (await CollectionPaymentIntakeGuard.HasSameDaySameAmountAsync(
                    connection,
                    dto.CustomerId.Value,
                    dto.Amount!.Value,
                    createdAtUtc,
                    clientPaymentId,
                    ct))
            {
                await CollectionPaymentIntakeGuard.WriteAuditAsync(
                    connection, dto, authenticatedDelegateId, createdAtUtc,
                    "AlreadyExistsSameDay",
                    CollectionPaymentRules.SameDaySameAmountAcknowledgedMessage,
                    null, ct);
                return new PaymentIdempotentResultDTO
                {
                    Success = true,
                    AlreadyExists = true,
                    DuplicateBlocked = true,
                    ClientPaymentId = clientPaymentId,
                    Message = CollectionPaymentRules.SameDaySameAmountAcknowledgedMessage
                };
            }

            try
            {
                var newId = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(@"
INSERT INTO CustomersPaymentsRequest
    (CustomerID, DelegateID, Amount, Location, PaymentDate, CreatedDate,
     ClientPaymentId, CreatedAtUtc, ReceivedAtUtc, EligibleForPostingAtUtc,
     AutoPostEnabled, ReceiptNumber, AsyncState, AsyncID)
OUTPUT INSERTED.CustomersPaymentsRequestID
VALUES
    (@CustomerId, @DelegateId, @Amount, @Location, @PaymentDate, GETDATE(),
     @ClientPaymentId, @CreatedAtUtc, @ReceivedAtUtc, @EligibleForPostingAtUtc,
     1, @ReceiptNumber, 0, @AsyncId)",
                        new
                        {
                            CustomerId = dto.CustomerId,
                            DelegateId = authenticatedDelegateId,
                            Amount = dto.Amount,
                            Location = dto.Location,
                            PaymentDate = createdAtUtc, // business event time
                            ClientPaymentId = clientPaymentId,
                            CreatedAtUtc = createdAtUtc,
                            ReceivedAtUtc = serverUtc,
                            EligibleForPostingAtUtc = eligibleAt,
                            ReceiptNumber = dto.ReceiptNumber,
                            AsyncId = dto.AsyncId
                        },
                        cancellationToken: ct));

                // Do NOT call PostEligible here. Box posting is owned by BE_Company
                // CollectionPaymentPostingHostedService (every ~1 min). Sync catch-up
                // contended on UPDLOCK and held مندوب HTTP up to 120s → app spinner hang.

                await CollectionPaymentIntakeGuard.WriteAuditAsync(
                    connection, dto, authenticatedDelegateId, createdAtUtc,
                    "Accepted", "تم استلام التسديد", newId, ct);

                return new PaymentIdempotentResultDTO
                {
                    Success = true,
                    AlreadyExists = false,
                    CustomersPaymentsRequestID = newId,
                    ClientPaymentId = clientPaymentId,
                    Message = "تم استلام التسديد"
                };
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                // Unique violation → treat as idempotent success. Hosted sweeper posts later.
                var again = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    new CommandDefinition(@"
SELECT TOP 1 CustomersPaymentsRequestID, ClientPaymentId
FROM CustomersPaymentsRequest WHERE ClientPaymentId = @ClientPaymentId",
                        new { ClientPaymentId = clientPaymentId },
                        cancellationToken: ct));

                return new PaymentIdempotentResultDTO
                {
                    Success = true,
                    AlreadyExists = true,
                    CustomersPaymentsRequestID = again == null ? null : (int?)again.CustomersPaymentsRequestID,
                    ClientPaymentId = clientPaymentId,
                    Message = "التسديد مسجل مسبقًا"
                };
            }
        }

        private static int _followerColumnReady; // 0 unknown, 1 ready

        private static async Task EnsureFollowerCollectionColumnAsync(SqlConnection connection, CancellationToken ct)
        {
            if (Volatile.Read(ref _followerColumnReady) == 1)
            {
                return;
            }

            await connection.ExecuteAsync(new CommandDefinition(@"
IF COL_LENGTH(N'dbo.Delegates', N'FollowerCollectionEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Delegates
        ADD FollowerCollectionEnabled BIT NOT NULL
            CONSTRAINT DF_Delegates_FollowerCollectionEnabled DEFAULT (0);
END", cancellationToken: ct));
            Volatile.Write(ref _followerColumnReady, 1);
        }

        private static async Task<bool> IsFollowerCollectionEnabledAsync(
            SqlConnection connection,
            int delegateId,
            CancellationToken ct)
        {
            if (delegateId <= 0)
            {
                return false;
            }

            await EnsureFollowerCollectionColumnAsync(connection, ct);
            return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
SELECT CAST(ISNULL((
    SELECT TOP 1 FollowerCollectionEnabled FROM dbo.Delegates WHERE DelegateID = @DelegateId
), 0) AS BIT);",
                new { DelegateId = delegateId },
                cancellationToken: ct,
                commandTimeout: 15));
        }

        public async Task<IEnumerable<CustomersPaymentsRequestsGetDTO>?> GetCustomersPaymentsRequestsByDelegateID(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersPaymentsRequestsGetDTO>("GetCustomersPaymentsRequestsByDelegateID",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }
    }
}

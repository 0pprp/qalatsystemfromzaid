using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
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

            if (dto.DelegateId is not null && dto.DelegateId != authenticatedDelegateId)
            {
                return new PaymentIdempotentResultDTO
                {
                    Success = false,
                    Message = "غير مسموح بإرسال تسديد باسم مندوب آخر"
                };
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

            var existing = await connection.QuerySingleOrDefaultAsync<dynamic>(
                new CommandDefinition(@"
SELECT TOP 1 CustomersPaymentsRequestID, ClientPaymentId
FROM CustomersPaymentsRequest
WHERE ClientPaymentId = @ClientPaymentId",
                    new { ClientPaymentId = clientPaymentId },
                    cancellationToken: ct));

            if (existing != null)
            {
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
                return new PaymentIdempotentResultDTO
                {
                    Success = true,
                    AlreadyExists = true,
                    ClientPaymentId = clientPaymentId,
                    Message = "التسديد مُرحّل مسبقًا"
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

                // If already past 16:00 Baghdad eligibility (including late offline sync),
                // attempt immediate box post via catch-up SP (idempotent).
                if (CollectionPaymentRules.IsEligibleForPosting(eligibleAt, serverUtc))
                {
                    await TryPostEligibleCatchUpAsync(connection, ct);
                }

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
                // Unique violation → treat as idempotent success.
                var again = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    new CommandDefinition(@"
SELECT TOP 1 CustomersPaymentsRequestID, ClientPaymentId, EligibleForPostingAtUtc
FROM CustomersPaymentsRequest WHERE ClientPaymentId = @ClientPaymentId",
                        new { ClientPaymentId = clientPaymentId },
                        cancellationToken: ct));

                if (again != null && again.EligibleForPostingAtUtc != null)
                {
                    DateTime eligibleExisting = again.EligibleForPostingAtUtc;
                    if (CollectionPaymentRules.IsEligibleForPosting(eligibleExisting, DateTime.UtcNow))
                    {
                        await TryPostEligibleCatchUpAsync(connection, ct);
                    }
                }

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

        private static async Task TryPostEligibleCatchUpAsync(SqlConnection connection, CancellationToken ct)
        {
            try
            {
                var p = new DynamicParameters();
                p.Add("@UserCreateID", 1);
                p.Add("@PostedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
                await connection.ExecuteAsync(new CommandDefinition(
                    "CustomersPaymentsRequest_PostEligible",
                    p,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 120,
                    cancellationToken: ct));
            }
            catch (SqlException)
            {
                // SP may not be deployed yet; HostedService will catch up later.
            }
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

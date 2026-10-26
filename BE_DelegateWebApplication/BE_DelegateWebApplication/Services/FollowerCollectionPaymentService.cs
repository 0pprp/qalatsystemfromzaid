using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.Services.CollectionPayments;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public interface IFollowerCollectionPaymentService
{
    Task EnsureReadyAsync(CancellationToken ct = default);

    Task<PaymentIdempotentResultDTO> PostPaymentAsync(
        int followerUserId,
        string? followerAsyncId,
        int listId,
        CustomersPaymentsRequestsPostDTO dto,
        CancellationToken ct = default);

    Task<IReadOnlyList<FollowerCollectionCustomerDto>> ListCustomersAsync(
        int followerUserId,
        int listId,
        CancellationToken ct = default);
}

public sealed class FollowerCollectionCustomerDto
{
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? ShopName { get; set; }
    public double AmountTotalSales { get; set; }
    public double AmountDaySales { get; set; }
    public double AmountRemaining { get; set; }
    public double ReceiptsTotal { get; set; }
    public string? CountReceiptDevice { get; set; }
    public int NumberOfDayPayment { get; set; }
    public string? ItemsNames { get; set; }
    public string? PhoneNumberCompany { get; set; }
    public double Amount1 { get; set; }
    public double Amount2 { get; set; }
    public double Amount3 { get; set; }
    public double Amount4 { get; set; }
    public double Amount5 { get; set; }
    public double Amount6 { get; set; }
    public double Amount7 { get; set; }
    public bool PaidToday { get; set; }
    /// <summary>مجموع تسديدات اليوم لهذا الزبون (دينار).</summary>
    public double PaidTodayAmount { get; set; }
}

/// <summary>
/// Follower collection intake: allowed only when list is linked AND FollowerCollectionEnabled.
/// Stores DelegateID = listId (customer list), never follower UserId.
/// </summary>
public sealed class FollowerCollectionPaymentService : IFollowerCollectionPaymentService
{
    public const string NotEnabledMessage = "جباية هذه القائمة غير مفعّلة للمتابع";
    public const string NotLinkedMessage = "القائمة غير مرتبطة بحسابك";

    private readonly string _cs;

    public FollowerCollectionPaymentService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureReadyAsync(CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        await EnsureCollectionColumnAsync(connection, ct);
    }

    public async Task<IReadOnlyList<FollowerCollectionCustomerDto>> ListCustomersAsync(
        int followerUserId,
        int listId,
        CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        await EnsureCollectionColumnAsync(connection, ct);

        var linked = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.FollowerUserLists WHERE UserId = @UserId AND ListId = @ListId;",
            new { UserId = followerUserId, ListId = listId }, cancellationToken: ct));
        if (linked == 0)
            throw new UnauthorizedAccessException(NotLinkedMessage);

        var enabled = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
SELECT CAST(ISNULL(FollowerCollectionEnabled, 0) AS BIT)
FROM dbo.Delegates WHERE DelegateID = @ListId;",
            new { ListId = listId }, cancellationToken: ct));
        if (!enabled)
            throw new InvalidOperationException(NotEnabledMessage);

        // Baghdad business day (03:00 roll) — keep same-day zeroed customers visible.
        // PaidToday / amount from CustomersPayments+AddToBox (reliable); never fall back to zeros.
        var iraqBizDate = CollectionPaymentRules.BaghdadBusinessDate(DateTime.UtcNow);
        var payBizExpr = string.Format(
            CollectionPaymentIntakeGuard.SqlBaghdadBusinessDateExpr.Trim(),
            "cp.PaymentDate");

        var sql = $@"
SELECT
    c.CustomerID AS CustomerId,
    ISNULL(vc.CustomerName, c.CustomerName) AS CustomerName,
    ISNULL(vc.PhoneNumber, c.PhoneNumber) AS PhoneNumber,
    ISNULL(vc.Address, c.Address) AS Address,
    ISNULL(vc.ShopName, c.ShopName) AS ShopName,
    CAST(ISNULL(vc.AmountTotalSales, 0) AS FLOAT) AS AmountTotalSales,
    CAST(ISNULL(vc.AmountDaySales, 0) AS FLOAT) AS AmountDaySales,
    CAST(ISNULL(vc.AmountRemaining, 0) AS FLOAT) AS AmountRemaining,
    CAST(ISNULL(vc.ReceiptsTotal, 0) AS FLOAT) AS ReceiptsTotal,
    CAST(ISNULL(vc.CountReceiptDevice, 0) AS NVARCHAR(32)) AS CountReceiptDevice,
    CAST(ISNULL(vc.NumberOfDayPayment, 0) AS INT) AS NumberOfDayPayment,
    ISNULL(vc.ItemsNames, N'') AS ItemsNames,
    ISNULL(vc.PhoneNumberCompany, N'') AS PhoneNumberCompany,
    CAST(ISNULL(vc.Amount1, 0) AS FLOAT) AS Amount1,
    CAST(ISNULL(vc.Amount2, 0) AS FLOAT) AS Amount2,
    CAST(ISNULL(vc.Amount3, 0) AS FLOAT) AS Amount3,
    CAST(ISNULL(vc.Amount4, 0) AS FLOAT) AS Amount4,
    CAST(ISNULL(vc.Amount5, 0) AS FLOAT) AS Amount5,
    CAST(ISNULL(vc.Amount6, 0) AS FLOAT) AS Amount6,
    CAST(ISNULL(vc.Amount7, 0) AS FLOAT) AS Amount7,
    CAST(CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.CustomersPayments cp
        WHERE cp.CustomerID = c.CustomerID
          AND {payBizExpr} = @IraqBizDate
    ) THEN 1 ELSE 0 END AS BIT) AS PaidToday,
    CAST(ISNULL((
        SELECT ROUND(SUM(ISNULL(b.Amount, 0) * 1448.0), -3)
        FROM dbo.CustomersPayments cp
        INNER JOIN dbo.AddToBox b ON b.CustomerPaymentID = cp.CustomerPaymentID
        WHERE cp.CustomerID = c.CustomerID
          AND {payBizExpr} = @IraqBizDate
    ), 0) AS FLOAT) AS PaidTodayAmount
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND {FollowerCollectionListRules.SqlIncludeCustomerPredicate}
ORDER BY ISNULL(vc.CustomerName, c.CustomerName); /* {FollowerCollectionListRules.DeployMarker} */";

        var rows = await connection.QueryAsync<FollowerCollectionCustomerDto>(new CommandDefinition(
            sql,
            new { ListId = listId, IraqBizDate = iraqBizDate.Date },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<PaymentIdempotentResultDTO> PostPaymentAsync(
        int followerUserId,
        string? followerAsyncId,
        int listId,
        CustomersPaymentsRequestsPostDTO dto,
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

        if (listId <= 0)
        {
            return new PaymentIdempotentResultDTO { Success = false, Message = "يجب اختيار قائمة" };
        }

        var serverUtc = DateTime.UtcNow;
        if (!CollectionPaymentRules.TryNormalizeCreatedAtUtc(dto.CreatedAtUtc, serverUtc, out var createdAtUtc, out var timeError))
        {
            return new PaymentIdempotentResultDTO { Success = false, Message = timeError };
        }

        var eligibleAt = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(createdAtUtc);
        var clientPaymentId = dto.ClientPaymentId!.Trim();

        await using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        await EnsureCollectionColumnAsync(connection, ct);

        var linked = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.FollowerUserLists WHERE UserId = @UserId AND ListId = @ListId;",
            new { UserId = followerUserId, ListId = listId }, cancellationToken: ct));
        if (linked == 0)
        {
            return new PaymentIdempotentResultDTO { Success = false, Message = NotLinkedMessage };
        }

        var enabled = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
SELECT CAST(ISNULL(FollowerCollectionEnabled, 0) AS BIT)
FROM dbo.Delegates WHERE DelegateID = @ListId;",
            new { ListId = listId }, cancellationToken: ct));
        if (!enabled)
        {
            return new PaymentIdempotentResultDTO { Success = false, Message = NotEnabledMessage };
        }

        var customerDelegateId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT DelegateID FROM Customers WHERE CustomerID = @CustomerId",
            new { CustomerId = dto.CustomerId }, cancellationToken: ct));
        if (customerDelegateId is null)
        {
            return new PaymentIdempotentResultDTO { Success = false, Message = "العميل غير موجود" };
        }

        if (customerDelegateId > 0 && customerDelegateId != listId)
        {
            return new PaymentIdempotentResultDTO
            {
                Success = false,
                Message = "الزبون لا ينتمي لهذه القائمة"
            };
        }

        var existing = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(@"
SELECT TOP 1 CustomersPaymentsRequestID, ClientPaymentId
FROM CustomersPaymentsRequest
WHERE ClientPaymentId = @ClientPaymentId",
            new { ClientPaymentId = clientPaymentId }, cancellationToken: ct));
        if (existing != null)
        {
            await CollectionPaymentIntakeGuard.WriteAuditAsync(
                connection, dto, listId, createdAtUtc,
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

        // Same-day same-amount: block second insert, but acknowledge success so devices
        // do not accumulate PermanentFailure while the first payment is already posted.
        if (await CollectionPaymentIntakeGuard.HasSameDaySameAmountAsync(
                connection,
                dto.CustomerId.Value,
                dto.Amount!.Value,
                createdAtUtc,
                clientPaymentId,
                ct))
        {
            await CollectionPaymentIntakeGuard.WriteAuditAsync(
                connection, dto, listId, createdAtUtc,
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
            var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
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
                    DelegateId = listId,
                    Amount = dto.Amount,
                    Location = dto.Location ?? "follower-collection",
                    PaymentDate = createdAtUtc,
                    ClientPaymentId = clientPaymentId,
                    CreatedAtUtc = createdAtUtc,
                    ReceivedAtUtc = serverUtc,
                    EligibleForPostingAtUtc = eligibleAt,
                    ReceiptNumber = dto.ReceiptNumber,
                    AsyncId = followerAsyncId
                },
                cancellationToken: ct));

            await CollectionPaymentIntakeGuard.WriteAuditAsync(
                connection, dto, listId, createdAtUtc,
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
            var again = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(@"
SELECT TOP 1 CustomersPaymentsRequestID, ClientPaymentId
FROM CustomersPaymentsRequest WHERE ClientPaymentId = @ClientPaymentId",
                new { ClientPaymentId = clientPaymentId }, cancellationToken: ct));
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

    private static async Task EnsureCollectionColumnAsync(SqlConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(@"
IF COL_LENGTH(N'dbo.Delegates', N'FollowerCollectionEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Delegates ADD FollowerCollectionEnabled BIT NOT NULL
        CONSTRAINT DF_Delegates_FollowerCollectionEnabled_Pay DEFAULT (0);
END", cancellationToken: ct));
    }
}

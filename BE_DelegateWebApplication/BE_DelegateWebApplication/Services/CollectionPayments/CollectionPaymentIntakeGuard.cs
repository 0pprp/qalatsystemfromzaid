using Dapper;
using BE_DelegateWebApplication.DTO;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services.CollectionPayments;

/// <summary>
/// Same-day same-amount duplicate guard + device audit writes for collection intake.
/// Deploy marker: SameDaySameAmountGuard_V2 (acknowledge duplicate as success)
/// </summary>
public static class CollectionPaymentIntakeGuard
{
    public const string DeployMarker = "SameDaySameAmountGuard_V2";
    public const string IntakeFastPathMarker = "IntakeNoSyncPostEligible_V1";

    private static int _auditTableReady; // 0 unknown, 1 ready

    /// <summary>
    /// Baghdad business-day expression for a UTC datetime column (PaymentDate / CreatedAtUtc).
    /// </summary>
    public const string SqlBaghdadBusinessDateExpr = @"
CASE
    WHEN DATEPART(HOUR, DATEADD(HOUR, 3, {0})) < 3
        THEN DATEADD(DAY, -1, CONVERT(date, DATEADD(HOUR, 3, {0})))
    ELSE CONVERT(date, DATEADD(HOUR, 3, {0}))
END";

    public static async Task EnsureAuditTableAsync(SqlConnection connection, CancellationToken ct)
    {
        if (Volatile.Read(ref _auditTableReady) == 1)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.DelegateDevicePaymentAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DelegateDevicePaymentAudit
    (
        AuditID BIGINT IDENTITY(1, 1) NOT NULL,
        ClientPaymentId NVARCHAR(36) NULL,
        CustomerID INT NULL,
        DelegateID INT NULL,
        Amount FLOAT NULL,
        GpsLocation NVARCHAR(200) NULL,
        DeviceId NVARCHAR(128) NULL,
        DeviceModel NVARCHAR(128) NULL,
        DevicePlatform NVARCHAR(64) NULL,
        AppVersion NVARCHAR(64) NULL,
        PaymentCreatedAtUtc DATETIME2(3) NULL,
        ReceivedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_DelegateDevicePaymentAudit_ReceivedAtUtc DEFAULT (SYSUTCDATETIME()),
        Outcome NVARCHAR(40) NOT NULL,
        OutcomeMessage NVARCHAR(400) NULL,
        CustomersPaymentsRequestID INT NULL,
        CONSTRAINT PK_DelegateDevicePaymentAudit PRIMARY KEY CLUSTERED (AuditID)
    );
END;", cancellationToken: ct));
        Volatile.Write(ref _auditTableReady, 1);
    }

    public static async Task WriteAuditAsync(
        SqlConnection connection,
        CustomersPaymentsRequestsPostDTO dto,
        int delegateId,
        DateTime? paymentCreatedAtUtc,
        string outcome,
        string? outcomeMessage,
        int? requestId,
        CancellationToken ct)
    {
        await EnsureAuditTableAsync(connection, ct);
        await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.DelegateDevicePaymentAudit
    (ClientPaymentId, CustomerID, DelegateID, Amount, GpsLocation,
     DeviceId, DeviceModel, DevicePlatform, AppVersion,
     PaymentCreatedAtUtc, ReceivedAtUtc, Outcome, OutcomeMessage, CustomersPaymentsRequestID)
VALUES
    (@ClientPaymentId, @CustomerID, @DelegateID, @Amount, @GpsLocation,
     @DeviceId, @DeviceModel, @DevicePlatform, @AppVersion,
     @PaymentCreatedAtUtc, SYSUTCDATETIME(), @Outcome, @OutcomeMessage, @RequestId);",
            new
            {
                ClientPaymentId = Trunc(dto.ClientPaymentId, 36),
                CustomerID = dto.CustomerId,
                DelegateID = delegateId,
                Amount = dto.Amount,
                GpsLocation = Trunc(dto.Location, 200),
                DeviceId = Trunc(dto.DeviceId, 128),
                DeviceModel = Trunc(dto.DeviceModel, 128),
                DevicePlatform = Trunc(dto.DevicePlatform, 64),
                AppVersion = Trunc(dto.AppVersion, 64),
                PaymentCreatedAtUtc = paymentCreatedAtUtc,
                Outcome = Trunc(outcome, 40) ?? "Unknown",
                OutcomeMessage = Trunc(outcomeMessage, 400),
                RequestId = requestId
            },
            cancellationToken: ct));
    }

    /// <summary>
    /// True when another payment (posted or queued) exists for same customer + amount on same Baghdad business day.
    /// Excludes the current ClientPaymentId (idempotent retries).
    /// </summary>
    public static async Task<bool> HasSameDaySameAmountAsync(
        SqlConnection connection,
        int customerId,
        double amountIqd,
        DateTime createdAtUtc,
        string clientPaymentId,
        CancellationToken ct)
    {
        var bizExprPay = string.Format(SqlBaghdadBusinessDateExpr, "cp.PaymentDate");
        var bizExprReq = string.Format(SqlBaghdadBusinessDateExpr, "ISNULL(r.CreatedAtUtc, r.PaymentDate)");
        var bizExprArg = string.Format(SqlBaghdadBusinessDateExpr, "@CreatedAtUtc");

        var sql = $@"
DECLARE @Biz date = {bizExprArg};

IF EXISTS (
    SELECT 1
    FROM dbo.CustomersPayments cp
    INNER JOIN dbo.AddToBox b ON b.CustomerPaymentID = cp.CustomerPaymentID
    WHERE cp.CustomerID = @CustomerId
      AND (cp.ClientPaymentId IS NULL OR cp.ClientPaymentId <> @ClientPaymentId)
      AND ABS(ISNULL(b.Amount, 0) * 1448.0 - @Amount) < 1.0
      AND {bizExprPay} = @Biz
)
    SELECT CAST(1 AS BIT);
ELSE IF EXISTS (
    SELECT 1
    FROM dbo.CustomersPaymentsRequest r
    WHERE r.CustomerID = @CustomerId
      AND (r.ClientPaymentId IS NULL OR r.ClientPaymentId <> @ClientPaymentId)
      AND ABS(ISNULL(r.Amount, 0) - @Amount) < 1.0
      AND {bizExprReq} = @Biz
)
    SELECT CAST(1 AS BIT);
ELSE
    SELECT CAST(0 AS BIT);";

        var hit = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new
            {
                CustomerId = customerId,
                Amount = amountIqd,
                CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc.ToUniversalTime(), DateTimeKind.Utc),
                ClientPaymentId = clientPaymentId
            },
            cancellationToken: ct,
            commandTimeout: 15));
        return hit;
    }

    private static string? Trunc(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }
}

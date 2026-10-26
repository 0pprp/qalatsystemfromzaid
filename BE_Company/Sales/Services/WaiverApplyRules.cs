namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Pure rules for waiver financial posting (idempotency + connection safety).
    /// </summary>
    public static class WaiverApplyRules
    {
        public const string AlreadyAppliedCode = "waiver_already_applied";

        public static bool IsValidWaiverId(Guid? waiverId) => waiverId is { } id && id != Guid.Empty;

        public static string ClaimSql => """
IF OBJECT_ID(N'dbo.SalesWaiverApplications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesWaiverApplications
    (
        WaiverId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SalesWaiverApplications PRIMARY KEY,
        CustomerId INT NOT NULL,
        Amount FLOAT NOT NULL,
        CustomerPaymentID INT NULL,
        DecisionId INT NULL,
        AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesWaiverApplications_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
        ActorEmployeeId INT NULL
    );
END
""";

        public static string TryClaimSql => """
INSERT INTO dbo.SalesWaiverApplications
    (WaiverId, CustomerId, Amount, CustomerPaymentID, DecisionId, AppliedAtUtc, ActorEmployeeId)
VALUES
    (@WaiverId, @CustomerId, @Amount, NULL, NULL, SYSUTCDATETIME(), @ActorEmployeeId);
""";

        public static string CompleteClaimSql => """
UPDATE dbo.SalesWaiverApplications
SET CustomerPaymentID = @CustomerPaymentID,
    DecisionId = @DecisionId
WHERE WaiverId = @WaiverId;
""";

        /// <summary>Discount path: no customer payment row.</summary>
        public static string CompleteDiscountClaimSql => """
UPDATE dbo.SalesWaiverApplications
SET CustomerPaymentID = NULL,
    DecisionId = @DecisionId
WHERE WaiverId = @WaiverId;
""";

        public static string ExistsSql => """
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.SalesWaiverApplications WITH (UPDLOCK, HOLDLOCK)
    WHERE WaiverId = @WaiverId) THEN 1 ELSE 0 END;
""";

        public static bool IsUniqueKeyViolation(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                if (cur is Microsoft.Data.SqlClient.SqlException sql
                    && (sql.Number == 2627 || sql.Number == 2601))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

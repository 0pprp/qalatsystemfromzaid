-- Open future EligibleForPostingAtUtc so PostEligible can claim rows waiting on the old 16:00 gate.
-- Safe / idempotent: only touches unposted requests with EligibleForPostingAtUtc in the future.
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.CustomersPaymentsRequest', N'U') IS NULL
BEGIN
    PRINT 'SKIP: CustomersPaymentsRequest missing';
    RETURN;
END;

UPDATE dbo.CustomersPaymentsRequest
SET EligibleForPostingAtUtc = COALESCE(ReceivedAtUtc, CreatedAtUtc, SYSUTCDATETIME())
WHERE EligibleForPostingAtUtc IS NOT NULL
  AND EligibleForPostingAtUtc > SYSUTCDATETIME()
  AND PostedToBoxAtUtc IS NULL;

PRINT CONCAT('Opened future EligibleForPostingAtUtc rows: ', @@ROWCOUNT);

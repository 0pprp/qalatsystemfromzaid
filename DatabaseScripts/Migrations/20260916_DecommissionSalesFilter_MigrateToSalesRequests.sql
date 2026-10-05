-- Idempotent: migrate Sales Filter queue rows into the sales-request path.
-- Run on EVERY branch Sales DB (نجف / بصرة / كرخ / رصافة / others).
-- Do NOT drop Filter* columns or SalesFilter* tables in this script.
-- Safe to re-run.
-- Does NOT INSERT new SalesRequests — only UPDATE existing rows in place (same Id).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NULL
BEGIN
    RAISERROR(N'SalesRequests table missing — wrong database.', 16, 1);
    RETURN;
END
GO

-- Gate: refuse accidental Production catalogs (name must contain DEMO)
IF DB_NAME() NOT LIKE N'%DEMO%'
BEGIN
    RAISERROR(N'Refused: DB_NAME() does not contain DEMO. Aborting.', 16, 1);
    RETURN;
END
GO

-- Snapshot counts before (for ops verification)
SELECT
    N'BEFORE' AS Phase,
    ISNULL(FilterStatus, N'(null)') AS FilterStatus,
    COUNT(1) AS RowCount
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
GROUP BY FilterStatus
ORDER BY FilterStatus;
GO

-- 1) PendingFilter + assigned → visible in employee طلبات البيع
UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE ISNULL(IsDeleted, 0) = 0
  AND TargetEmployeeId > 0
  AND FilterStatus = N'PendingFilter';
GO

-- 2) OnHold → ReadyForSale; assigned rows become Status=Assigned; preserve hold note
--    Unassigned OnHold: do not invent assignee; only clear queue gate + preserve notes.
UPDATE dbo.SalesRequests
SET
    Status = CASE WHEN TargetEmployeeId > 0 THEN N'Assigned' ELSE Status END,
    FilterStatus = N'ReadyForSale',
    Notes = CASE
        WHEN FilterNote IS NOT NULL AND LTRIM(RTRIM(FilterNote)) <> N''
             AND CHARINDEX(N'[تعليق فلترة]', ISNULL(Notes, N'')) = 0
            THEN LEFT(
                CASE
                    WHEN Notes IS NULL OR LTRIM(RTRIM(Notes)) = N''
                        THEN N'[تعليق فلترة] ' + LTRIM(RTRIM(FilterNote))
                    ELSE Notes + NCHAR(10) + N'[تعليق فلترة] ' + LTRIM(RTRIM(FilterNote))
                END,
                1000)
        ELSE Notes
    END,
    PendingNote = CASE
        WHEN (PendingNote IS NULL OR LTRIM(RTRIM(PendingNote)) = N'')
             AND FilterNote IS NOT NULL AND LTRIM(RTRIM(FilterNote)) <> N''
            THEN LEFT(LTRIM(RTRIM(FilterNote)), 1000)
        ELSE PendingNote
    END
WHERE ISNULL(IsDeleted, 0) = 0
  AND FilterStatus = N'OnHold';
GO

-- 3) Filter Rejected → sales Rejected; preserve reject reason (same Id, no insert)
UPDATE dbo.SalesRequests
SET
    Status = N'Rejected',
    FilterStatus = N'ReadyForSale',
    RejectionReason = CASE
        WHEN (RejectionReason IS NULL OR LTRIM(RTRIM(RejectionReason)) = N'')
             AND FilterRejectReason IS NOT NULL AND LTRIM(RTRIM(FilterRejectReason)) <> N''
            THEN LTRIM(RTRIM(FilterRejectReason))
        ELSE RejectionReason
    END
WHERE ISNULL(IsDeleted, 0) = 0
  AND FilterStatus = N'Rejected';
GO

-- 4) Safety net: remaining assigned rows not yet ReadyForSale → ReadyForSale
--    Does not touch unassigned (TargetEmployeeId <= 0).
UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE ISNULL(IsDeleted, 0) = 0
  AND TargetEmployeeId > 0
  AND (FilterStatus IS NULL OR FilterStatus <> N'ReadyForSale');
GO

-- Snapshot counts after
SELECT
    N'AFTER' AS Phase,
    ISNULL(FilterStatus, N'(null)') AS FilterStatus,
    Status,
    COUNT(1) AS RowCount
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
GROUP BY FilterStatus, Status
ORDER BY FilterStatus, Status;
GO

-- Expect zero active filter-queue states for assigned work
SELECT
    N'REMAINING_ASSIGNED_QUEUE' AS CheckName,
    COUNT(1) AS RowCount
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
  AND TargetEmployeeId > 0
  AND FilterStatus IN (N'PendingFilter', N'OnHold', N'Rejected');
GO

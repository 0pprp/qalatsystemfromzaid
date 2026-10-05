-- Idempotent PRODUCTION migration: Sales Filter queue → sales-request path.
-- UPDATE only (same SalesRequests.Id). No INSERT. No DELETE. No DROP.
-- Refuse DEMO catalogs. Refuse master/tempdb/model/msdb.

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

DECLARE @db sysname = DB_NAME();
IF @db LIKE N'%DEMO%'
   OR @db IN (N'master', N'tempdb', N'model', N'msdb')
   OR @db NOT LIKE N'DatabaseCompany%'
BEGIN
    RAISERROR(N'Refused: not an allowed Production branch catalog (DatabaseCompany*, non-DEMO).', 16, 1);
    RETURN;
END
GO

SELECT N'BEFORE' AS Phase, ISNULL(FilterStatus, N'(null)') AS FilterStatus, COUNT(1) AS [RowCount]
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
GROUP BY FilterStatus
ORDER BY FilterStatus;
GO

UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE ISNULL(IsDeleted, 0) = 0
  AND TargetEmployeeId > 0
  AND FilterStatus = N'PendingFilter';
GO

-- Notes is NVARCHAR(1000). Unbounded append of FilterNote can raise truncation
-- (ANSI_WARNINGS ON) after earlier batches already committed. Bound with LEFT.
-- Idempotent: skip when [تعليق فلترة] marker already present.
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

UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE ISNULL(IsDeleted, 0) = 0
  AND TargetEmployeeId > 0
  AND (FilterStatus IS NULL OR FilterStatus <> N'ReadyForSale');
GO

-- Unassigned filter-queue rows (no employee yet) also leave the filter gate
UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE ISNULL(IsDeleted, 0) = 0
  AND FilterStatus IN (N'PendingFilter', N'OnHold');
GO

SELECT N'AFTER' AS Phase, ISNULL(FilterStatus, N'(null)') AS FilterStatus, Status, COUNT(1) AS [RowCount]
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
GROUP BY FilterStatus, Status
ORDER BY FilterStatus, Status;
GO

SELECT N'REMAINING_ASSIGNED_QUEUE' AS CheckName, COUNT(1) AS [RowCount]
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
  AND TargetEmployeeId > 0
  AND FilterStatus IN (N'PendingFilter', N'OnHold', N'Rejected');
GO

CREATE OR ALTER PROC [dbo].[GetDateWeek]
AS
BEGIN
    SET NOCOUNT ON;
    -- Date1 = Baghdad business today … Date7 = 6 days ago (dd/MM).
    -- Aligned with View_WeekFinal Amount1..Amount7 (03:00 Baghdad boundary).

    DECLARE @BaghdadNow DATETIME2(3) = DATEADD(HOUR, 3, SYSUTCDATETIME());
    DECLARE @Today DATE =
        CASE
            WHEN DATEPART(HOUR, @BaghdadNow) < 3
                THEN DATEADD(DAY, -1, CONVERT(date, @BaghdadNow))
            ELSE CONVERT(date, @BaghdadNow)
        END;

    SELECT
        CONVERT(nvarchar(5), @Today, 3) AS Date1,
        CONVERT(nvarchar(5), DATEADD(DAY, -1, @Today), 3) AS Date2,
        CONVERT(nvarchar(5), DATEADD(DAY, -2, @Today), 3) AS Date3,
        CONVERT(nvarchar(5), DATEADD(DAY, -3, @Today), 3) AS Date4,
        CONVERT(nvarchar(5), DATEADD(DAY, -4, @Today), 3) AS Date5,
        CONVERT(nvarchar(5), DATEADD(DAY, -5, @Today), 3) AS Date6,
        CONVERT(nvarchar(5), DATEADD(DAY, -6, @Today), 3) AS Date7;
END

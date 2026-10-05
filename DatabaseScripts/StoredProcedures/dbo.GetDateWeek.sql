CREATE OR ALTER PROC [dbo].[GetDateWeek]
AS
-- Date1 = today … Date7 = 6 days ago (aligned with View_WeekFinal Amount1..Amount7)
SELECT
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()), 120) AS Date1,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-1), 120) AS Date2,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-2), 120) AS Date3,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-3), 120) AS Date4,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-4), 120) AS Date5,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-5), 120) AS Date6,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-6), 120) AS Date7;

CREATE proc [dbo].[GetDateWeek]
as
select 
CONVERT(nvarchar,CONVERT(date,GETDATE()-1)) as Date1,
CONVERT(nvarchar,CONVERT(date,GETDATE()-2)) as Date2,
CONVERT(nvarchar,CONVERT(date,GETDATE()-3)) as Date3,
CONVERT(nvarchar,CONVERT(date,GETDATE()-4)) as Date4,
CONVERT(nvarchar,CONVERT(date,GETDATE()-5)) as Date5,
CONVERT(nvarchar,CONVERT(date,GETDATE()-6)) as Date6,
CONVERT(nvarchar,CONVERT(date,GETDATE()-7)) as Date7


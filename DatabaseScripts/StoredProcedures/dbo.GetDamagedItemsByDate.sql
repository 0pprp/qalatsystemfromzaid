CREATE proc [dbo].[GetDamagedItemsByDate]
@FromDate datetime ,
@ToDate datetime
as
select * from View_DamagedItems
where 
CONVERT(date, DamagedItemDate)>=@FromDate
and
CONVERT(date, DamagedItemDate)<=@ToDate


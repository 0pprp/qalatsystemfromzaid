CREATE proc [dbo].[GetBuysItemsByDate]
@FromDate datetime,
@ToDate datetime
as
SELECT   *  FROM           View_BuysItems
where     CONVERT(date, DateCreate)>=@FromDate  and   CONVERT(date, DateCreate)<=@ToDate


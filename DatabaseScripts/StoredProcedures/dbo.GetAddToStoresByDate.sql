CREATE proc [dbo].[GetAddToStoresByDate]
@FromDate datetime,
@ToDate datetime
as
SELECT        * FROM      View_AddToStores
where CONVERT(date, DateAddToStore)>=@FromDate and CONVERT(date, DateAddToStore)<=@ToDate


CREATE proc [dbo].[GetAddToStoresStoreByDate]
@StoreID int = NULL,
@FromDate datetime,
@ToDate datetime
as
SELECT        * FROM      View_AddToStores
where StoreID=@StoreID and CONVERT(date, DateAddToStore)>=@FromDate and CONVERT(date, DateAddToStore)<=@ToDate


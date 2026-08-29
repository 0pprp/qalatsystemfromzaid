CREATE proc [dbo].[GetBuysItemsByStoreID]
@StoreID int = NULL
as
 SELECT   *  FROM           View_BuysItems

where     StoreID=@StoreID


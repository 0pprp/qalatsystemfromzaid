CREATE proc [dbo].[GetStoreAsyncID]
@StoreID int = NULL
as
select top 1   AsyncID from Stores where StoreID=@StoreID


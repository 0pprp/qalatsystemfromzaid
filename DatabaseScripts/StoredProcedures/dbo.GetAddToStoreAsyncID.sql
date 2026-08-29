CREATE proc [dbo].[GetAddToStoreAsyncID]
@AddToStoreID int = NULL
as
select AsyncID from AddToStores where AddToStoreID=@AddToStoreID


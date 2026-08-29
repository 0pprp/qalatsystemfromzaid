CREATE proc [dbo].[DeleteAddToStoreAsyncID]
@AddToStoreID int = NULL
as
Insert into DeleteData (AddToStoresAsyncID) values ((select AsyncID from AddToStores where AddToStoreID=@AddToStoreID))


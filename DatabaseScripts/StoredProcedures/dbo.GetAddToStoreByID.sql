CREATE proc [dbo].[GetAddToStoreByID]
@AddToStoreID int = NULL
as
select AddToStoreID,Quantity from View_AddToStores where AddToStoreID=@AddToStoreID


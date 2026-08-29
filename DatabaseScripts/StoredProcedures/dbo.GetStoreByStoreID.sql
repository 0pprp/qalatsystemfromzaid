CREATE proc [dbo].[GetStoreByStoreID]
@StoreID int = NULL
as
select * from Stores where StoreID=@StoreID


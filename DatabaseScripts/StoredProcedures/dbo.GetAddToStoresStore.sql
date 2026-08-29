CREATE proc [dbo].[GetAddToStoresStore] 
@StoreID int  = NULL
as
SELECT        * FROM      View_AddToStores
where StoreID=@StoreID  


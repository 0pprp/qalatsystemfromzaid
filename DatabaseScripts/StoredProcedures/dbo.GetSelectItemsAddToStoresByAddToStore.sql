CREATE proc [dbo].[GetSelectItemsAddToStoresByAddToStore]
@AddToStoreID int = NULL
as
select * from View_SelectItemsAddToStores
where AddToStoreID=@AddToStoreID


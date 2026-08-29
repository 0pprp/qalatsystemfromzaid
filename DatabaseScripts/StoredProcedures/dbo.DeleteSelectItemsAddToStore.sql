CREATE proc [dbo].[DeleteSelectItemsAddToStore]
@SelectItemAddToStoreID int  = NULL
as
exec DeleteSelectItemsAddToStoresAsyncID @SelectItemAddToStoreID=@SelectItemAddToStoreID
delete from SelectItemsAddToStores
where  SelectItemAddToStoreID=@SelectItemAddToStoreID


 


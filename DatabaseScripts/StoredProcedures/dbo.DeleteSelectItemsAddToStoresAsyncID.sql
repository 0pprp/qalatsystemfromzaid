CREATE proc [dbo].[DeleteSelectItemsAddToStoresAsyncID]
@SelectItemAddToStoreID int = NULL
as
insert into DeleteData (SelectItemsAddToStoresAsyncID) values ((select AsyncID from SelectItemsAddToStores where SelectItemAddToStoreID=@SelectItemAddToStoreID))


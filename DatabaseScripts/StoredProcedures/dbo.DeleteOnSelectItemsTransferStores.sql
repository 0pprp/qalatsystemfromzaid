CREATE proc [dbo].[DeleteOnSelectItemsTransferStores]
@SelectItemTransferStoreID int = NULL
as
insert into DeleteData (SelectItemsTransferStoresAsyncID)
values ((select AsyncID from SelectItemsTransferStores where SelectItemTransferStoreID=@SelectItemTransferStoreID))


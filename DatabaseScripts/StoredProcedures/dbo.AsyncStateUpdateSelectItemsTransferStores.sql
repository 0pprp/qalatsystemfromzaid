
CREATE proc [dbo].[AsyncStateUpdateSelectItemsTransferStores]
@SelectItemTransferStoreID int = NULL
as
update SelectItemsTransferStores set AsyncState='true' where SelectItemTransferStoreID=@SelectItemTransferStoreID


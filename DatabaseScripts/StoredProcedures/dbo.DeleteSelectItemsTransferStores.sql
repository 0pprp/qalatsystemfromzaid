CREATE proc [dbo].[DeleteSelectItemsTransferStores]
@SelectItemTransferStoreID int = NULL
as
exec DeleteOnSelectItemsTransferStores @SelectItemTransferStoreID=@SelectItemTransferStoreID
delete from SelectItemsTransferStores
where SelectItemTransferStoreID=@SelectItemTransferStoreID
 


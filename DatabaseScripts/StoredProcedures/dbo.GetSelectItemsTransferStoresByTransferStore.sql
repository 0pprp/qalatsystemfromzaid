CREATE proc [dbo].[GetSelectItemsTransferStoresByTransferStore]
@TransferStoreID int = NULL
as
select * from View_SelectItemsTransferStores
where TransferStoreID=@TransferStoreID

 


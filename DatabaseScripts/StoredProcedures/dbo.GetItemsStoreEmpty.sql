CREATE proc [dbo].[GetItemsStoreEmpty]
@StoreID int = NULL
as
SELECT     * FROM      View_Items
where  ItemState='true' and StoreID=@StoreID and Quantity=0


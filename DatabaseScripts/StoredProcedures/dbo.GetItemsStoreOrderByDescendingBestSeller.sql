CREATE proc [dbo].[GetItemsStoreOrderByDescendingBestSeller]
@StoreID int = NULL
as
SELECT     * FROM      View_Items
where  ItemState='true' and StoreID=@StoreID  order by NumberOfItemsSales desc


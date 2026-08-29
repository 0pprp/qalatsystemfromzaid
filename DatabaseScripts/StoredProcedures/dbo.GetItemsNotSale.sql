CREATE proc  [dbo].[GetItemsNotSale]
@StoreID int = NULL
as
SELECT     * FROM      View_Items
where  ItemState='true' and NumberOfItemsSales=0 and StoreID=@StoreID 





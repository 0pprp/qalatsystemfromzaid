CREATE proc [dbo].[GetItemsStore]
@StoreID int = NULL
as
SELECT     * FROM      View_Items
where  ItemState='true' and StoreID=@StoreID
 


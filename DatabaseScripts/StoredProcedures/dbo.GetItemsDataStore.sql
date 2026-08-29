CREATE proc [dbo].[GetItemsDataStore]
@StoreID int = NULL
as
SELECT     * FROM      View_ItemsData
where  ItemState='true' and StoreID=@StoreID
 


 
CREATE proc [dbo].[GetItemsStoreByName]
@StoreID int = NULL,
@ItemName nvarchar(255)
as
SELECT     * FROM      View_Items where  ItemState='true' and StoreID=@StoreID and  ItemName like  N'%'+@ItemName+N'%'
 


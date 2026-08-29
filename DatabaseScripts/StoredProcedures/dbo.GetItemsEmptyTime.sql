CREATE proc  [dbo].[GetItemsEmptyTime]
@StoreID int = NULL
as
SELECT     * FROM      View_Items
where  ItemState='true' and StoreID=@StoreID  and Quantity<NotificationNumber and Quantity>0 





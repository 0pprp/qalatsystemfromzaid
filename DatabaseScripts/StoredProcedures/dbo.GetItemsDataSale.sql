CREATE proc [dbo].[GetItemsDataSale]
@StoreID int = NULL,
@TextSearch nvarchar(255)
as
select * from View_ItemsData where ItemState='true' and StoreID=@StoreID and Quantity>0 and ItemName like N'%'+@TextSearch+N'%'


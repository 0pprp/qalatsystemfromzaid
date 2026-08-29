CREATE proc [dbo].[GetItemsData]
@StoreID int = NULL,
@TextSearch nvarchar(255)
as
select * from View_ItemsData where StoreID=@StoreID and ItemName like N'%'+@TextSearch+N'%'


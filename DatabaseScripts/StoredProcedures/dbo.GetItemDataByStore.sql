CREATE proc [dbo].[GetItemDataByStore]
@StoreID int = NULL
as
select * from View_ItemsData where StoreID=@StoreID


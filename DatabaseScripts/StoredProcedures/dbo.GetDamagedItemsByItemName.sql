CREATE proc [dbo].[GetDamagedItemsByItemName]
@ItemName nvarchar(255)
as
select * from View_DamagedItems
where 
ItemsNames like N'%'+@ItemName+N'%'


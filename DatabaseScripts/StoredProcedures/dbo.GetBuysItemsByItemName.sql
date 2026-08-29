CREATE proc [dbo].[GetBuysItemsByItemName]
@ItemName nvarchar(255)
as
  SELECT   *  FROM           View_BuysItems

where 
 ItemName like N'%'+@ItemName+N'%' 


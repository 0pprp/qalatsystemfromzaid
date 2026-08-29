CREATE proc [dbo].[GetBuysByItemName]
@ItemName nvarchar(255)
as
SELECT   * from View_Buys

where  ItemsNames like N'%'+@ItemName+N'%'


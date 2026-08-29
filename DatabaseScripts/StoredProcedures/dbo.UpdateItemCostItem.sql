CREATE proc [dbo].[UpdateItemCostItem]
@ItemID int = NULL,
@ItemCost float
as
update Items set ItemCost=@ItemCost where ItemID=@ItemID


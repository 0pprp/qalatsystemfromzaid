CREATE proc [dbo].[UpdateItemPriceItem]
@ItemID int = NULL,
@ItemPrice float
as
update Items set ItemPrice=@ItemPrice where ItemID=@ItemID


CREATE proc [dbo].[UpdateItemStateServer]
@ItemID int =null,
@ItemState bit = null
as
update Items set ItemState=@ItemState where ItemID=@ItemID


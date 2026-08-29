CREATE proc [dbo].[UpdateQuantityItem]
@ItemID int = NULL,
@Quantity int = NULL
as
update Items set Quantity=@Quantity where ItemID=@ItemID


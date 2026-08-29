
CREATE proc [dbo].[AsyncStateUpdateItems]
@ItemID int = NULL
as
update Items set AsyncState='true' where ItemID=@ItemID


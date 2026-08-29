
CREATE proc [dbo].[AsyncStateUpdateBoxes]
@BoxID int = NULL
as
update Boxes set AsyncState='true' where BoxID=@BoxID


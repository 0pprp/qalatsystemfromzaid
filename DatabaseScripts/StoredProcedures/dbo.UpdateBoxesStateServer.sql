

CREATE proc [dbo].[UpdateBoxesStateServer]
@BoxID int =null,
@BoxState bit = null
as
update Boxes set BoxState=@BoxState where BoxID=@BoxID


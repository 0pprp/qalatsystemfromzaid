
CREATE proc [dbo].[AsyncStateUpdateUsers]
@UserID int = NULL
as
update Users set AsyncState='true' where UserID=@UserID


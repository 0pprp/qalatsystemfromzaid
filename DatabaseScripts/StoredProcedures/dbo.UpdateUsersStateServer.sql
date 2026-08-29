

CREATE proc [dbo].[UpdateUsersStateServer]
@UserID int =null,
@UserState bit = null
as
update Users set UserState=@UserState where UserID=@UserID


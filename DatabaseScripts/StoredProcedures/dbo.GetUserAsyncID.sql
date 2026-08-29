CREATE proc [dbo].[GetUserAsyncID]
@UserID int = NULL
as
select top 1   AsyncID from Users where UserID=@UserID


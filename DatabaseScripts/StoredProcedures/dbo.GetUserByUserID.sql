CREATE proc [dbo].[GetUserByUserID]
@UserID  int = NULL
as 
select * from Users
where UserID=@UserID


CREATE proc [dbo].[Users_GetUserLogin]
@UserName nvarchar(100),
@Password nvarchar(100)
as
select * from Users where UserName=@UserName and Password=@Password and UserState='true'


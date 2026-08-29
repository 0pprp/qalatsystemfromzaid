CREATE proc [dbo].[GetUserLogin]
@UserName  nvarchar(255),
@Password  nvarchar(255)
as 
select * from Users  
where UserState='true' and UserName=@UserName and Password=@Password


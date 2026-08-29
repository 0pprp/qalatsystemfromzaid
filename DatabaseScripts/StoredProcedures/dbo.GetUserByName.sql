CREATE proc [dbo].[GetUserByName]
@UserName nvarchar(255)
as 
select * from Users
where UserState='true' and UserName like N'%'+@UserName+N'%'


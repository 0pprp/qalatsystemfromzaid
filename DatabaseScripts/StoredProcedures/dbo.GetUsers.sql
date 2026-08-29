CREATE proc [dbo].[GetUsers]
as 
select * from Users where UserState='true'


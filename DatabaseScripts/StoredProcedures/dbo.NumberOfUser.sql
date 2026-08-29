CREATE proc [dbo].[NumberOfUser]
as
select count(*) as NumberOfUser from Users where UserState='true'


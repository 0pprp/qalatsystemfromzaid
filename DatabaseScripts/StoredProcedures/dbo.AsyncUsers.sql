CREATE proc [dbo].[AsyncUsers]
as
select * from Users where AsyncState='false'


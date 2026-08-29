CREATE proc [dbo].[AsyncEmployees]
as
select * from Employees where AsyncState='false'


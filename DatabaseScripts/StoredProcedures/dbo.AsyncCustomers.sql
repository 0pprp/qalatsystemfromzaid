CREATE proc [dbo].[AsyncCustomers]
as
select * from Customers where AsyncState='false'


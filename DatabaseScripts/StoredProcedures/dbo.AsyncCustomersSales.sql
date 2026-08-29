CREATE proc [dbo].[AsyncCustomersSales]
as
select * from CustomersSales where AsyncState='false'


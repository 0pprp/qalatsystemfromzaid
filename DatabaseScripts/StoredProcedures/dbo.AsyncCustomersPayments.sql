CREATE proc [dbo].[AsyncCustomersPayments]
as
select * from CustomersPayments where AsyncState='false'


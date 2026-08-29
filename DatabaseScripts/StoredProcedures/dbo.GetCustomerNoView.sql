CREATE proc [dbo].[GetCustomerNoView]
as
select * from Customers where CustomerState='true'


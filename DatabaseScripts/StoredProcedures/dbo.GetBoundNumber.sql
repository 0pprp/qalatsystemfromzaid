CREATE proc [dbo].[GetBoundNumber]
as
select count(*)+1 as Count from CustomersPayments 


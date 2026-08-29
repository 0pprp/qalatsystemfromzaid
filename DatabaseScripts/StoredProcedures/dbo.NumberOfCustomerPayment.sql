CREATE proc [dbo].[NumberOfCustomerPayment]
as
select count(*) as NumberOfCustomerPayment from CustomersPayments 


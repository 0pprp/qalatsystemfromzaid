CREATE proc [dbo].[LastCustomerPaymentID]
as
select top 1 CustomerPaymentID from CustomersPayments order by CustomerPaymentID desc


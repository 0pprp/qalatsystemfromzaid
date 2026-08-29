CREATE proc [dbo].[GetCustomersPayments]
as
select * from View_CustomersPayments order by CustomerPaymentID


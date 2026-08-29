CREATE proc [dbo].[CustomersPaymentAllData]
as
select CustomerPaymentID,CustomerID,PaymentDate,AsyncID from CustomersPayments


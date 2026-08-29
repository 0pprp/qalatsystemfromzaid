 
CREATE proc [dbo].[NumberOfCustomerPaymentMonth]
as
select count(*)as NumberOfCustomerPaymentMonth from View_CustomersPayments where PaymentDate>=(SELECT CONVERT(date, GETUTCDATE() - 30) AS date) 


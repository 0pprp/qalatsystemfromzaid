 
CREATE proc [dbo].[NumberOfCustomerPaymentWeek]
as
select count(*)as NumberOfCustomerPaymentWeek from View_CustomersPayments where PaymentDate>=(SELECT CONVERT(date, GETUTCDATE() - 7) AS date) 


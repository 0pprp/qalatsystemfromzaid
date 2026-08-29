CREATE proc [dbo].[GetCustomersPaymenByID]
@CustomersPaymentID int = NULL
as
select * from View_CustomersPayments where CustomerPaymentID=@CustomersPaymentID


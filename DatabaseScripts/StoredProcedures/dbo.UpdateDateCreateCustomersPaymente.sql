CREATE proc [dbo].[UpdateDateCreateCustomersPaymente]
@CustomerPaymentID int = NULL,
@DateCreate datetime
as
update CustomersPayments set PaymentDate=@DateCreate where CustomerPaymentID=@CustomerPaymentID


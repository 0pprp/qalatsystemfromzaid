CREATE proc [dbo].[UpdateDateCreateAndPaymentRequest]
@CustomersPaymentsRequestID int = NULL,
@DateCreate datetime ,
@Amount float
as
update CustomersPaymentsRequest set PaymentDate=@DateCreate,Amount=@Amount
where CustomersPaymentsRequestID=@CustomersPaymentsRequestID


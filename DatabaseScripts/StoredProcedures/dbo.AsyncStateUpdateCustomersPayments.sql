
CREATE proc [dbo].[AsyncStateUpdateCustomersPayments]
@CustomerPaymentID int = NULL
as
update CustomersPayments set AsyncState='true' where CustomerPaymentID=@CustomerPaymentID


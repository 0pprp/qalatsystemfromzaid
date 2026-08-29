 
CREATE proc [dbo].[PostSelectPaymentCustomerTemporary]
@CustomerID int,
@DelegateID int,
@Amount float,
@Location nvarchar(100)
as
insert into CustomersPaymentsRequest (CustomerID,DelegateID,Amount,Location,PaymentDate) values (@CustomerID,@DelegateID,@Amount,@Location,GETDATE())


CREATE proc [dbo].[GetReceiptCustomerDate]
@CustomerID int,
@PaymentDate datetime
as
select * from View_ReceiptCustomerDate where CustomerID = @CustomerID and PaymentDate = @PaymentDate


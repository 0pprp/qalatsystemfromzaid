create proc [dbo].[CustomersPaymentsRequest_ChangeDate]
@PaymentDate datetime  
as
update CustomersPaymentsRequest set PaymentDate=@PaymentDate


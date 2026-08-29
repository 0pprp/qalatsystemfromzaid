CREATE proc [dbo].[GetCustomersPaymentsCustomerDate]
@CustomerID int,
@DateCreate datetime
as
select * from View_CustomersPayments where CustomerID=@CustomerID and CONVERT(date,PaymentDate)=CONVERT(date,@DateCreate)


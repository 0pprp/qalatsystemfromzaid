CREATE proc [dbo].[GetCustomersPaymenByCustomerByFromDate]
@Date datetime,
@CustomerName nvarchar(255)
as
select * from View_CustomersPayments where CustomerName like N'%'+@CustomerName+N'%' and CONVERT(date, PaymentDate)=@Date  order by CustomerPaymentID


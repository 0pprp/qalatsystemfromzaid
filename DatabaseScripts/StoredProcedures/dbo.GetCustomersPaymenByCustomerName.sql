CREATE proc [dbo].[GetCustomersPaymenByCustomerName]
@CustomerName nvarchar(255)
as
select * from View_CustomersPayments
where  
CustomerName like  N'%'+@CustomerName+N'%'  order by CustomerPaymentID


 CREATE proc [dbo].[GetCustomerByCustomerName]
 @CustomerName nvarchar(255)
 as
   select * from View_CustomersDelegate where CustomerState='true'
and CustomerName like N'%'+@CustomerName+N'%'  order by CustomerID


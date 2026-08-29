 
 CREATE proc [dbo].[GetAllCustomerByCustomerName]
 @CustomerName nvarchar(255)
 as
select * from View_CustomersDelegate
where CustomerState='true' and CustomerName like N'%'+@CustomerName+N'%' or  PhoneNumber like N'%'+@CustomerName+N'%' or SaleName like N'%'+@CustomerName+N'%'
 


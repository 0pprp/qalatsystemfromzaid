 CREATE proc [dbo].[GetCustomersSalesByCustomerName]
 @CustomerName nvarchar(255) 
 as
 select * from View_CustomersSales
 where CustomerName  like  N'%'+@CustomerName+N'%' or ItemsNames like N'%'+@CustomerName+N'%'


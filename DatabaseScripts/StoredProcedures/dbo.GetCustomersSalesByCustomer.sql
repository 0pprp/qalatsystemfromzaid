 CREATE proc [dbo].[GetCustomersSalesByCustomer]
 @CustomerID int =null
 as
 select * from View_CustomersSales
 where CustomerID=@CustomerID


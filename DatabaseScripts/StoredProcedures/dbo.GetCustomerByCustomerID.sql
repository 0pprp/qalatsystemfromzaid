 CREATE proc [dbo].[GetCustomerByCustomerID]
 @CustomerID int =null
 as
  select * from View_CustomersDelegate where 
  CustomerID=@CustomerID


CREATE proc [dbo].[GetCustomersSalesCustomer]
@CustomerID int
as
select * from View_CustomersSales where CustomerID = @CustomerID 


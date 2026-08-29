create   proc [dbo].[GetCustomerInfoByID]
@CustomerID int
as
select * from View_CustomersInfoSimple where CustomerID = @CustomerID


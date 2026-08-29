CREATE proc [dbo].[GetCustomersDataInfo]
@CustomerID int
as
select * from View_Customers where CustomerID=@CustomerID


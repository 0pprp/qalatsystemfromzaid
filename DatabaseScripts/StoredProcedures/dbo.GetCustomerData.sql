CREATE proc [dbo].[GetCustomerData]
@CustomerID int = NULL
as
select * from View_CustomersDelegate
where CustomerID=@CustomerID


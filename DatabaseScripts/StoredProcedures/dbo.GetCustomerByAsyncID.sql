CREATE proc [dbo].[GetCustomerByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 CustomerID from Customers where AsyncID=@AsyncID


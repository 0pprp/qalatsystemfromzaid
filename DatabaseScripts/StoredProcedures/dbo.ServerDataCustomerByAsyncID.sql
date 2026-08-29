CREATE proc [dbo].[ServerDataCustomerByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Customers where AsyncID=@AsyncID


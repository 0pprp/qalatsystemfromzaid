CREATE proc [dbo].[ServerDataCustomerSaleAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from CustomersSales where AsyncID=@AsyncID


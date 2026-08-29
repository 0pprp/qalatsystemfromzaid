CREATE proc [dbo].[ServerDataSupplierByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Suppliers where AsyncID=@AsyncID


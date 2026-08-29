CREATE proc [dbo].[GetSupplierAsyncID]
@SupplierID int = NULL
as
select top 1 AsyncID from Suppliers where SupplierID=@SupplierID


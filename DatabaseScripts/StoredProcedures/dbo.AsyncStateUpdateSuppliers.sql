
CREATE proc [dbo].[AsyncStateUpdateSuppliers]
@SupplierID int = NULL
as
update Suppliers set AsyncState='true' where SupplierID=@SupplierID


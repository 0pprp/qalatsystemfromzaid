

CREATE proc [dbo].[UpdateSupplierStateServer]
@SupplierID int =null,
@SupplierState bit = null
as
update Suppliers set SupplierState=@SupplierState where SupplierID=@SupplierID


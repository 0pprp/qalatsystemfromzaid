CREATE proc [dbo].[GetSupplierByID]
@SupplierID int = NULL
as
select * from View_Suppliers where SupplierState='true' and SupplierID=@SupplierID


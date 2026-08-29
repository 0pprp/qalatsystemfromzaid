CREATE proc [dbo].[GetSuppliers]
 
as
select * from View_Suppliers where SupplierState='true' 


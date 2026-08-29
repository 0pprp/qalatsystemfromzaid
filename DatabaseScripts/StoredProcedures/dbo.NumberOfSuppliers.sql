 
CREATE proc [dbo].[NumberOfSuppliers]
as
select count(*) as NumberOfSuppliers from  Suppliers where SupplierState='true'



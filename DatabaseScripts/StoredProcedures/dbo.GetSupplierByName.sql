CREATE proc [dbo].[GetSupplierByName] 
@SupplierName nvarchar(255) 
as
select * from View_Suppliers
where SupplierState='true' and SupplierName like N'%'+@SupplierName+N'%'


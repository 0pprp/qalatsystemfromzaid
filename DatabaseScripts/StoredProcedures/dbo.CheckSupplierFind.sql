CREATE proc [dbo].[CheckSupplierFind]
@SupplierName nvarchar(255)
as
select * from Suppliers where SupplierState='true' and SupplierName=@SupplierName


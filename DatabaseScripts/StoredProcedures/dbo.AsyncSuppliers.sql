CREATE proc [dbo].[AsyncSuppliers]
as
select * from Suppliers where AsyncState='false'


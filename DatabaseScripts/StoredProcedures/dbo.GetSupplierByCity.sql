CREATE proc [dbo].[GetSupplierByCity]
@CityID int = NULL
as
select * from View_Suppliers  where SupplierState='true' and CityID=@CityID


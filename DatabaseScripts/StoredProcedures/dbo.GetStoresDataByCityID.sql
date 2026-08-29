CREATE proc [dbo].[GetStoresDataByCityID]
@CityID int = NULL
as
select * from Stores where CityID=@CityID


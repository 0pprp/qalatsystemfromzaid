CREATE proc [dbo].[GetCityAsyncID]
@CityID int = NULL
as
select top 1   AsyncID from Cities where CityID=@CityID


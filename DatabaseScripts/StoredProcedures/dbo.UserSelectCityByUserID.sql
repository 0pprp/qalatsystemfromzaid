CREATE proc [dbo].[UserSelectCityByUserID]
@UserID int = NULL
as
select CityID ,(select CityName from Cities where CityID=UsersSelectedCities.CityID)as CityName from UsersSelectedCities where UserID=@UserID


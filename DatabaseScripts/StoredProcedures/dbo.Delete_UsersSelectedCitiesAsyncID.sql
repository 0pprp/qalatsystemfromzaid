
CREATE proc [dbo].[Delete_UsersSelectedCitiesAsyncID]  @AsyncID nvarchar(255) = null as delete from UsersSelectedCities where AsyncID=@AsyncID 



CREATE proc [dbo].[Delete_SelectedCitiesAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectedCities where AsyncID=@AsyncID 


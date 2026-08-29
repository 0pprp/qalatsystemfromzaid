
CREATE proc [dbo].[Delete_CitiesAsyncID]  @AsyncID nvarchar(255) = null as delete from Cities where AsyncID=@AsyncID 


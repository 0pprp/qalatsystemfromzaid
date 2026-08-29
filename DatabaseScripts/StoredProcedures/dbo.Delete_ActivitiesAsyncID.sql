CREATE proc [dbo].[Delete_ActivitiesAsyncID]  @AsyncID nvarchar(255) = null as delete from Activities where AsyncID=@AsyncID 



CREATE proc [dbo].[Delete_AddToBoxAsyncID]  @AsyncID nvarchar(255) = null as delete from AddToBox where AsyncID=@AsyncID 


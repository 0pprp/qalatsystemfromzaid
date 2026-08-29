CREATE proc [dbo].[Delete_CategoryAsyncID]  @AsyncID nvarchar(255) = null as delete from Category where AsyncID=@AsyncID 


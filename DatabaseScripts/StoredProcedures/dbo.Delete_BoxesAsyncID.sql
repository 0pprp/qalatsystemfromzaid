
CREATE proc [dbo].[Delete_BoxesAsyncID]  @AsyncID nvarchar(255) = null as delete from Boxes where AsyncID=@AsyncID 


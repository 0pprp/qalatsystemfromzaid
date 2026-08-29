
CREATE proc [dbo].[Delete_StoresAsyncID]  @AsyncID nvarchar(255) = null as delete from Stores where AsyncID=@AsyncID 


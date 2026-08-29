
CREATE proc [dbo].[Delete_DocumentsAsyncID]  @AsyncID nvarchar(255) = null as delete from Documents where AsyncID=@AsyncID 


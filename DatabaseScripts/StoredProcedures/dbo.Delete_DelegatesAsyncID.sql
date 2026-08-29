
CREATE proc [dbo].[Delete_DelegatesAsyncID]  @AsyncID nvarchar(255) = null as delete from Delegates where AsyncID=@AsyncID 


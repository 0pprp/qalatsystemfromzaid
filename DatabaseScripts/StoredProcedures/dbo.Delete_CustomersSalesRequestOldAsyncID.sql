
CREATE proc [dbo].[Delete_CustomersSalesRequestOldAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomersSalesRequestOld where AsyncID=@AsyncID 


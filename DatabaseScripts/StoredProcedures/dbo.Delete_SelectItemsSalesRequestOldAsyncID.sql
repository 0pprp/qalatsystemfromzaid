
CREATE proc [dbo].[Delete_SelectItemsSalesRequestOldAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsSalesRequestOld where AsyncID=@AsyncID 


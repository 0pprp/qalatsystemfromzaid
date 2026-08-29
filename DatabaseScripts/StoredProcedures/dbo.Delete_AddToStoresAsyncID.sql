
CREATE proc [dbo].[Delete_AddToStoresAsyncID]  @AsyncID nvarchar(255) = null as delete from AddToStores where AsyncID=@AsyncID 


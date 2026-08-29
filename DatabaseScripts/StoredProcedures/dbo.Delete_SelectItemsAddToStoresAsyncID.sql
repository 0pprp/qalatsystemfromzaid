
CREATE proc [dbo].[Delete_SelectItemsAddToStoresAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsAddToStores where AsyncID=@AsyncID 


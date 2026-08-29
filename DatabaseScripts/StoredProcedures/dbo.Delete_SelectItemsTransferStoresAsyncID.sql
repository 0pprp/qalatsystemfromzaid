
CREATE proc [dbo].[Delete_SelectItemsTransferStoresAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsTransferStores where AsyncID=@AsyncID 


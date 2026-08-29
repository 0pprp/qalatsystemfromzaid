
CREATE proc [dbo].[Delete_TransferStoresAsyncID]  @AsyncID nvarchar(255) = null as delete from TransferStores where AsyncID=@AsyncID 


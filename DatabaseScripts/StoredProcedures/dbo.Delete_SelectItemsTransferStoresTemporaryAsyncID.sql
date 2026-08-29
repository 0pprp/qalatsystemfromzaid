
CREATE proc [dbo].[Delete_SelectItemsTransferStoresTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsTransferStoresTemporary where AsyncID=@AsyncID 


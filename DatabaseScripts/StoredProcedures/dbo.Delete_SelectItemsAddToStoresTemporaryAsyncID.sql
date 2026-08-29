
CREATE proc [dbo].[Delete_SelectItemsAddToStoresTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsAddToStoresTemporary where AsyncID=@AsyncID 



CREATE proc [dbo].[Delete_WithdrawalStoresAsyncID]  @AsyncID nvarchar(255) = null as delete from WithdrawalStores where AsyncID=@AsyncID 



CREATE proc [dbo].[Delete_StoreBalanceAsyncID]  @AsyncID nvarchar(255) = null as delete from StoreBalance where AsyncID=@AsyncID 


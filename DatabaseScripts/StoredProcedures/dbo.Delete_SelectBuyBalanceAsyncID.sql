
CREATE proc [dbo].[Delete_SelectBuyBalanceAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectBuyBalance where AsyncID=@AsyncID 


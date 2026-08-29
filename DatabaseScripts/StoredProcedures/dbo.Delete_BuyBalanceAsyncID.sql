
CREATE proc [dbo].[Delete_BuyBalanceAsyncID]  @AsyncID nvarchar(255) = null as delete from BuyBalance where AsyncID=@AsyncID 


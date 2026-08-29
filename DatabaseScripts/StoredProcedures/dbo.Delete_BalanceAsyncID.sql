
CREATE proc [dbo].[Delete_BalanceAsyncID]  @AsyncID nvarchar(255) = null as delete from Balance where AsyncID=@AsyncID 


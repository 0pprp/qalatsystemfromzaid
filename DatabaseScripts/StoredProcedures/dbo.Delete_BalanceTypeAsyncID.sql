
CREATE proc [dbo].[Delete_BalanceTypeAsyncID]  @AsyncID nvarchar(255) = null as delete from BalanceType where AsyncID=@AsyncID 


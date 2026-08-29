
CREATE proc [dbo].[Delete_WithdrawalFromBoxAsyncID]  @AsyncID nvarchar(255) = null as delete from WithdrawalFromBox where AsyncID=@AsyncID 


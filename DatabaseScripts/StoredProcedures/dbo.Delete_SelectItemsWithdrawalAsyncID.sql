
CREATE proc [dbo].[Delete_SelectItemsWithdrawalAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsWithdrawal where AsyncID=@AsyncID 


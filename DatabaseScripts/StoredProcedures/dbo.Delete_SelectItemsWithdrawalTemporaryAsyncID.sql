
CREATE proc [dbo].[Delete_SelectItemsWithdrawalTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsWithdrawalTemporary where AsyncID=@AsyncID 


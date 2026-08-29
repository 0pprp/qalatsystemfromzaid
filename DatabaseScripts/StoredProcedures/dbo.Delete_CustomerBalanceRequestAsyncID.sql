
CREATE proc [dbo].[Delete_CustomerBalanceRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerBalanceRequest where AsyncID=@AsyncID 


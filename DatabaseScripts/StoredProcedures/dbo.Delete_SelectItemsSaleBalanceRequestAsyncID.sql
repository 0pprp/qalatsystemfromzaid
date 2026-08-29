
CREATE proc [dbo].[Delete_SelectItemsSaleBalanceRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsSaleBalanceRequest where AsyncID=@AsyncID 


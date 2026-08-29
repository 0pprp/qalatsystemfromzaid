
CREATE proc [dbo].[Delete_SelectItemsSaleBalanceOldRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsSaleBalanceOldRequest where AsyncID=@AsyncID 


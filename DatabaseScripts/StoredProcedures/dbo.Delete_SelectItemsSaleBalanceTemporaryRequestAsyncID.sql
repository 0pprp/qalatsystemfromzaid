
CREATE proc [dbo].[Delete_SelectItemsSaleBalanceTemporaryRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsSaleBalanceTemporaryRequest where AsyncID=@AsyncID 


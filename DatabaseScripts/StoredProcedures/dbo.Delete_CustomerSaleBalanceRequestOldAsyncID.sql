
CREATE proc [dbo].[Delete_CustomerSaleBalanceRequestOldAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerSaleBalanceRequestOld where AsyncID=@AsyncID 


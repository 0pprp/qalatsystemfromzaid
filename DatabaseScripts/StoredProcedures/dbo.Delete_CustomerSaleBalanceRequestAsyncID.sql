
CREATE proc [dbo].[Delete_CustomerSaleBalanceRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerSaleBalanceRequest where AsyncID=@AsyncID 



CREATE proc [dbo].[Delete_CustomerSaleBalanceAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerSaleBalance where AsyncID=@AsyncID 


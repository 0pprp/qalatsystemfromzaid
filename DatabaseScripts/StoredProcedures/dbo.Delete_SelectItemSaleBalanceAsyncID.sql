
CREATE proc [dbo].[Delete_SelectItemSaleBalanceAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemSaleBalance where AsyncID=@AsyncID 


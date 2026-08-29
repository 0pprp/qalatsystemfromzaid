
CREATE proc [dbo].[Delete_ExchangeItemsDebtsAsyncID]  @AsyncID nvarchar(255) = null as delete from ExchangeItemsDebts where AsyncID=@AsyncID 



CREATE proc [dbo].[Delete_ExchangeItemsAsyncID]  @AsyncID nvarchar(255) = null as delete from ExchangeItems where AsyncID=@AsyncID 


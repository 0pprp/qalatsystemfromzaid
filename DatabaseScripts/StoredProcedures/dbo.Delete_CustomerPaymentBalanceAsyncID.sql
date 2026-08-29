
CREATE proc [dbo].[Delete_CustomerPaymentBalanceAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerPaymentBalance where AsyncID=@AsyncID 


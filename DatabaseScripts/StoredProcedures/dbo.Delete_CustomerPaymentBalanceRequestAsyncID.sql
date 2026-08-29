
CREATE proc [dbo].[Delete_CustomerPaymentBalanceRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerPaymentBalanceRequest where AsyncID=@AsyncID 


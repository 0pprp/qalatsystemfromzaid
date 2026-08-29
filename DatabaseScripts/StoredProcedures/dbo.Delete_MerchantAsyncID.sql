
CREATE proc [dbo].[Delete_MerchantAsyncID]  @AsyncID nvarchar(255) = null as delete from Merchant where AsyncID=@AsyncID 


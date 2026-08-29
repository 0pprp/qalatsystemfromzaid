
CREATE proc [dbo].[Delete_ItemMerchantImageTempAsyncID]  @AsyncID nvarchar(255) = null as delete from ItemMerchantImageTemp where AsyncID=@AsyncID 


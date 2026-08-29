
CREATE proc [dbo].[Delete_ItemMerchantImageAsyncID]  @AsyncID nvarchar(255) = null as delete from ItemMerchantImage where AsyncID=@AsyncID 


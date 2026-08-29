
CREATE proc [dbo].[Delete_ItemMerchantAsyncID]  @AsyncID nvarchar(255) = null as delete from ItemMerchant where AsyncID=@AsyncID 


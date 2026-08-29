
CREATE proc [dbo].[Delete_MerchantNotificationAsyncID]  @AsyncID nvarchar(255) = null as delete from MerchantNotification where AsyncID=@AsyncID 


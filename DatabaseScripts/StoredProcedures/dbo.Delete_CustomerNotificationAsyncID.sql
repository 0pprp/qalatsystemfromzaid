
CREATE proc [dbo].[Delete_CustomerNotificationAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerNotification where AsyncID=@AsyncID 


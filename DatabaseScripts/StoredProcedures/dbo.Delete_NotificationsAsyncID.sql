
CREATE proc [dbo].[Delete_NotificationsAsyncID]  @AsyncID nvarchar(255) = null as delete from Notifications where AsyncID=@AsyncID 


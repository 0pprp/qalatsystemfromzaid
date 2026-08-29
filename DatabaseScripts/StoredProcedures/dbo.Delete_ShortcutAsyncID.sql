
CREATE proc [dbo].[Delete_ShortcutAsyncID]  @AsyncID nvarchar(255) = null as delete from Shortcut where AsyncID=@AsyncID 



CREATE proc [dbo].[Delete_PermissionsAsyncID]  @AsyncID nvarchar(255) = null as delete from Permissions where AsyncID=@AsyncID 


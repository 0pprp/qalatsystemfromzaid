
CREATE proc [dbo].[Delete_PermissionsTypesAsyncID]  @AsyncID nvarchar(255) = null as delete from PermissionsTypes where AsyncID=@AsyncID 


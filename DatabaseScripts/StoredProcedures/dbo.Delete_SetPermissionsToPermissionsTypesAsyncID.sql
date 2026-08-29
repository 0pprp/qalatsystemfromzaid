
CREATE proc [dbo].[Delete_SetPermissionsToPermissionsTypesAsyncID]  @AsyncID nvarchar(255) = null as delete from SetPermissionsToPermissionsTypes where AsyncID=@AsyncID 


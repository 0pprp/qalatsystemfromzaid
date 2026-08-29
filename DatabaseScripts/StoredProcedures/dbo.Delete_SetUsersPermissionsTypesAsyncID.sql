
CREATE proc [dbo].[Delete_SetUsersPermissionsTypesAsyncID]  @AsyncID nvarchar(255) = null as delete from SetUsersPermissionsTypes where AsyncID=@AsyncID 


CREATE proc [dbo].[GetPermissionUser]
@UserID int = NULL,
@PermissionName nvarchar(255),
@GroupName nvarchar(255)
as
DECLARE @PermissionTypeID INT = (select top 1 PermissionTypeID from SetUsersPermissionsTypes where UserID=@UserID)
select (@UserID)as UserID, (select  GroupID from Permissions where PermissionID=SetPermissionsToPermissionsTypes.PermissionID)as GroupID,PermissionTypeID,  PermissionID,(select UserName from Users where UserID=@UserID)as UserName, (select (select GroupName from Groups where GroupID=Permissions.GroupID) from Permissions where PermissionID=SetPermissionsToPermissionsTypes.PermissionID)as GroupName,(select PermissionTypeName from PermissionsTypes where PermissionTypeID=SetPermissionsToPermissionsTypes.PermissionTypeID)as PermissionTypeName,(select PermissionState from PermissionsTypes where PermissionTypeID=SetPermissionsToPermissionsTypes.PermissionTypeID)as PermissionState,(select PermissionName from Permissions where PermissionID=SetPermissionsToPermissionsTypes.PermissionID)as PermissionName  from SetPermissionsToPermissionsTypes where PermissionTypeID=@PermissionTypeID and
(select PermissionName from Permissions where PermissionID=SetPermissionsToPermissionsTypes.PermissionID)=@PermissionName and
(select (select GroupName from Groups where GroupID=Permissions.GroupID) from Permissions where PermissionID=SetPermissionsToPermissionsTypes.PermissionID)=@GroupName


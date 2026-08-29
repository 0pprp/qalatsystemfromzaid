CREATE proc [dbo].[DeletePermissionDelegate]
@DelegateID int = NULL
as
delete from SelectDelegate where DelegateChildID=@DelegateID


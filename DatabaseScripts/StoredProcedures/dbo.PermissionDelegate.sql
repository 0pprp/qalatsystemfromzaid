CREATE proc [dbo].[PermissionDelegate]
@DelegateID int = NULL
as
select DelegateID, DelegateName,AsyncID,(select count(*)  from  SelectDelegate where DelegateFatherID=@DelegateID and DelegateChildID=Delegates.DelegateID)as Number from Delegates where DelegateState='true'


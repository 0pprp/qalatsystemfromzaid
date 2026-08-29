CREATE proc [dbo].[ClearPermissionDelegate]
@DelegateID int =null
as
delete from SelectDelegate where DelegateFatherID=@DelegateID


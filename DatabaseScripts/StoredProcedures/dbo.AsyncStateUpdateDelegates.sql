
CREATE proc [dbo].[AsyncStateUpdateDelegates]
@DelegateID int = NULL
as
update Delegates set AsyncState='true' where DelegateID=@DelegateID


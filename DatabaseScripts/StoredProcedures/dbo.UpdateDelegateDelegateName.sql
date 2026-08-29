CREATE proc [dbo].[UpdateDelegateDelegateName]
@DelegateID int = NULL,
@DelegateName nvarchar(255)
as
update Delegates set DelegateName=@DelegateName where DelegateID=@DelegateID


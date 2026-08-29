
CREATE proc [dbo].[UpdateDelegateUpdateReceipt]
@DelegateID int = NULL,
@UpdateReceipt nvarchar(255)
as
update Delegates set UpdateReceipt=@UpdateReceipt where DelegateID=@DelegateID



CREATE proc [dbo].[UpdateDelegateDeleteReceipt]
@DelegateID int = NULL,
@DeleteReceipt nvarchar(255)
as
update Delegates set DeleteReceipt=@DeleteReceipt where DelegateID=@DelegateID


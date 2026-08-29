
CREATE proc [dbo].[UpdateDelegateReceiptName]
@DelegateID int = NULL,
@ReceiptName nvarchar(255)
as
update Delegates set ReceiptName=@ReceiptName where DelegateID=@DelegateID


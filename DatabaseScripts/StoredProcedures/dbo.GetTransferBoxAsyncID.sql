CREATE proc [dbo].[GetTransferBoxAsyncID]
@TransferBoxID int = NULL
as
select AsyncID from TransferBoxs where TransferBoxID=@TransferBoxID


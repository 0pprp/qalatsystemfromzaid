CREATE proc [dbo].[DeleteTransferBoxIDTransferBoxsAsyncID]
@TransferBoxID int = NULL
as
 Insert into DeleteData (TransferBoxsAsyncID) values 
 ((select AsyncID from TransferBoxs where TransferBoxID=@TransferBoxID))


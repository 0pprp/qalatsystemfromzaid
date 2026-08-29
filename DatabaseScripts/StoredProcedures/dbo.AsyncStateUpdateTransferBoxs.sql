
CREATE proc [dbo].[AsyncStateUpdateTransferBoxs]
@TransferBoxID int = NULL
as
update TransferBoxs set AsyncState='true' where TransferBoxID=@TransferBoxID


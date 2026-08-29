CREATE proc [dbo].[UpdateDateCreateAddToBoxAmount]
@AddToBoxID int = NULL,
@Amount float
as
update AddToBox set Amount=@Amount where AddToBoxID=@AddToBoxID


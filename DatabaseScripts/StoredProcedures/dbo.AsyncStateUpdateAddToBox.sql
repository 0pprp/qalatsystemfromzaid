CREATE proc [dbo].[AsyncStateUpdateAddToBox]
@AddToBoxID int = NULL
as
update AddToBox set AsyncState='true' where AddToBoxID=@AddToBoxID


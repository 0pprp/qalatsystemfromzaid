CREATE proc [dbo].[GetItemAsyncID]
@ItemID int = NULL
as
select AsyncID from Items where ItemID=@ItemID


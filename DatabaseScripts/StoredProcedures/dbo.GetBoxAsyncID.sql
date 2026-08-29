CREATE proc [dbo].[GetBoxAsyncID]
@BoxID int = NULL
as
select top 1   AsyncID from Boxes where BoxID=@BoxID


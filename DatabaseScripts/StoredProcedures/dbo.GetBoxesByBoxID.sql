CREATE proc [dbo].[GetBoxesByBoxID]
@BoxID int = NULL
as
select * from View_Box where BoxID=@BoxID


create proc [dbo].[Boxs_GetByBoxID]
@BoxID int
as
select * from View_Box where BoxID=@BoxID


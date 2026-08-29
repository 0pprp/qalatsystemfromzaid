CREATE proc [dbo].[GetAddToBoxByBox]
@BoxID int  = NULL
as
SELECT     * from View_AddToBox

where BoxID=@BoxID  


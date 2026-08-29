CREATE proc [dbo].[GetBoxesByName]
@BoxName nvarchar(255)
as
SELECT    * FROM     View_Box
WHERE        (BoxState = 'true') and BoxName  like N'%'+@BoxName+N'%' order by BoxID


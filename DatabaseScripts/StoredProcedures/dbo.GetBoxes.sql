CREATE proc [dbo].[GetBoxes]
as
SELECT    * FROM     View_Box
WHERE        (BoxState = 'true') order by BoxID


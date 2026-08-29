CREATE proc [dbo].[DeleteActivity]
@ActivityID int = NULL
as
DELETE FROM [dbo].[Activities]
      WHERE ActivityID=@ActivityID


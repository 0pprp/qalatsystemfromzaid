/*
  تطبيق المتابع: القوائم المرتبطة من SelectDelegate
  يُنفَّذ على قاعدة كل محافظة.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROC [dbo].[Followers_IsLinked]
    @FatherID INT,
    @ChildID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.SelectDelegate
        WHERE DelegateFatherID = @FatherID
          AND DelegateChildID = @ChildID
    ) THEN 1 ELSE 0 END AS IsLinked;
END
GO

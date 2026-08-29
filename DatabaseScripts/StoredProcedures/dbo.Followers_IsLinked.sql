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

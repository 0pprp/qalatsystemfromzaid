-- Deduplicate child lists for the delegate app sync (fixes repeated customers in المتوقفين / الغير مسددين).
-- No mobile APK update required. Apply on every branch DB used by Delegate* APIs.

CREATE OR ALTER PROC [dbo].[GetDelegateSelect]
@DelegateID int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.*
    FROM dbo.View_SelectDelegate AS v
    INNER JOIN (
        SELECT
            DelegateID,
            MIN(SelectDelegateID) AS SelectDelegateID
        FROM dbo.View_SelectDelegate
        WHERE DelegateFatherID = @DelegateID
        GROUP BY DelegateID
    ) AS d ON d.SelectDelegateID = v.SelectDelegateID;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_ReadAllDecisionNotifications]
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE DecisionNotifications
    SET IsRead = 1
    WHERE IsRead = 0;

    SELECT 1 AS Success;
END

CREATE OR ALTER PROC [dbo].[Customers_ReadDecisionNotification]
    @NotificationID INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE DecisionNotifications
    SET IsRead = 1
    WHERE NotificationID = @NotificationID;

    SELECT 1 AS Success;
END

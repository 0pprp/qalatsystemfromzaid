CREATE OR ALTER PROC [dbo].[Customers_GetDecisionNotifications]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        N.NotificationID,
        N.DecisionID,
        N.IsRead,
        N.CreatedDate,
        D.CustomerID,
        D.DecisionType,
        D.WeekPaid,
        D.PaidPercent,
        D.Note,
        C.CustomerName,
        U.UserName
    FROM DecisionNotifications N
    INNER JOIN CustomerWeekDecisions D ON D.DecisionID = N.DecisionID
    INNER JOIN Customers C ON C.CustomerID = D.CustomerID
    INNER JOIN Users U ON U.UserID = D.UserID
    ORDER BY N.CreatedDate DESC;
END

create   view [dbo].[View_CustomerNotification]
AS
SELECT        dbo.CustomerNotification.CustomerNotificationID, dbo.CustomerNotification.UserID, dbo.CustomerNotification.CustomerID, dbo.CustomerNotification.Description, dbo.CustomerNotification.IsRead, 
                         dbo.CustomerNotification.AsyncID, dbo.CustomerNotification.AsyncState, dbo.CustomerNotification.TypeNotification, dbo.CustomerNotification.Title, ISNULL(dbo.Users.UserName, '') AS UserName, 
                         ISNULL(dbo.Customers.CustomerName, '') AS CustomerName
FROM            dbo.CustomerNotification LEFT OUTER JOIN
                         dbo.Users ON dbo.CustomerNotification.UserID = dbo.Users.UserID LEFT OUTER JOIN
                         dbo.Customers ON dbo.CustomerNotification.CustomerID = dbo.Customers.CustomerID


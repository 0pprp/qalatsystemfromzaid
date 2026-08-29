create   view [dbo].[View_MerchantNotification]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), MerchantData AS
    (SELECT        MerchantID, MerchantName
      FROM            dbo.Merchant
      WHERE        (MerchantState = 'true'))
    SELECT        dbo.MerchantNotification.MerchantNotificationID, dbo.MerchantNotification.UserID, dbo.MerchantNotification.MerchantID, dbo.MerchantNotification.Description, dbo.MerchantNotification.IsRead, 
                              dbo.MerchantNotification.AsyncID, dbo.MerchantNotification.AsyncState, dbo.MerchantNotification.TypeNotification, dbo.MerchantNotification.Title, UserData_1.UserName, MerchantData_1.MerchantName
     FROM            dbo.MerchantNotification LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.MerchantNotification.UserID = UserData_1.UserID LEFT OUTER JOIN
                              MerchantData AS MerchantData_1 ON dbo.MerchantNotification.MerchantID = MerchantData_1.MerchantID


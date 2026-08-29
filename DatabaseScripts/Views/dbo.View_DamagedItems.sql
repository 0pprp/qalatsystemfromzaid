create   view [dbo].[View_DamagedItems]
AS
SELECT        DamagedItemID, UserID, Reason, DamagedItemDate, State, AsyncState, AsyncID,
                             (SELECT        UserName
                               FROM            dbo.Users
                               WHERE        (UserID = dbo.DamagedItems.UserID)) AS UserName,
                             (SELECT        + ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS nvarchar(255)) + ' ) ' + ' ) '
                               FROM            View_SelectDamagedItems
                               WHERE        (DamagedItemID = dbo.DamagedItems.DamagedItemID) FOR XML PATH('')) AS ItemsNames
FROM            dbo.DamagedItems


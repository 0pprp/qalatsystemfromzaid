create   view [dbo].[View_WithdrawalStores]
AS
SELECT 
    WS.WithdrawalStoresID,
    WS.UserID,
    WS.State,
    WS.WithdrawalStoresDate,
    WS.StoreID,
    WS.AsyncState,
    WS.AsyncID,
    U.UserName,
    S.StoreName,
    STUFF((
        SELECT 
            ' ( ' + '' + IW.ItemName + '' + ' ( ' + CAST(IW.Quantity AS NVARCHAR(255)) + ' ) ' + ' ) '
        FROM 
            dbo.View_SelectItemsWithdrawal AS IW
        WHERE 
            IW.WithdrawalStoresID = WS.WithdrawalStoresID
        FOR XML PATH('')
    ), 1, 0, '') AS ItemsNames,
    ISNULL(SUM(IW.Quantity), 0) AS Quantity
FROM 
    dbo.WithdrawalStores AS WS
LEFT JOIN 
    dbo.Users AS U ON WS.UserID = U.UserID
LEFT JOIN 
    dbo.Stores AS S ON WS.StoreID = S.StoreID
LEFT JOIN 
    dbo.View_SelectItemsWithdrawal AS IW ON WS.WithdrawalStoresID = IW.WithdrawalStoresID
GROUP BY 
    WS.WithdrawalStoresID,
    WS.UserID,
    WS.State,
    WS.WithdrawalStoresDate,
    WS.StoreID,
    WS.AsyncState,
    WS.AsyncID,
    U.UserName,
    S.StoreName;



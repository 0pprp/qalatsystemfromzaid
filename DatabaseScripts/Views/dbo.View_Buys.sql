CREATE view [dbo].[View_Buys]
as
SELECT 
    Buys.BuyID, 
    Buys.UserID, 
    Buys.SupplierID, 
    Buys.BoundNumber, 
    Buys.Recipient, 
    Buys.Notes, 
    Buys.DateCreate, 
    Buys.DateModify, 
    Buys.StoreID, 
    Buys.BuyState, 
    Buys.BoxID, 
    Buys.AsyncState, 
    Buys.AsyncID,
    Users.UserName AS UserName,
    Suppliers.SupplierName AS SupplierName,
    Suppliers.CityID AS CityID,
    Cities.CityName AS CityName,
    Stores.StoreName AS StoreName,
    Boxes.BoxName AS BoxName,
    (SELECT ISNULL(SUM(ItemTotalCostDenar), 0) 
     FROM View_BuysItems 
     WHERE View_BuysItems.BuyID = Buys.BuyID
    ) AS TotalAmountDenar,
    (SELECT ISNULL(SUM(AmountDenar), 0) 
     FROM View_WithdrawalFromBox 
     WHERE View_WithdrawalFromBox.BuyID = Buys.BuyID
    ) AS AmountSpentDenar,
    (SELECT ISNULL(SUM(Quantity), 0) 
     FROM View_BuysItems 
     WHERE View_BuysItems.BuyID = Buys.BuyID
    ) AS NumberOfItemsBuys,
    STUFF((
        SELECT 
            ' ( ' + ItemName + ' ( ' + CAST(Quantity AS NVARCHAR(255)) + ' ) )'
        FROM 
            dbo.View_BuysItems AS InnerView
        WHERE 
            InnerView.BuyID = Buys.BuyID
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS ItemsNames

FROM 
    dbo.Buys
LEFT JOIN 
    dbo.Users ON Buys.UserID = Users.UserID
LEFT JOIN 
    dbo.Suppliers ON Buys.SupplierID = Suppliers.SupplierID
LEFT JOIN 
    dbo.Cities ON Suppliers.CityID = Cities.CityID
LEFT JOIN 
    dbo.Stores ON Buys.StoreID = Stores.StoreID
LEFT JOIN 
    dbo.Boxes ON Buys.BoxID = Boxes.BoxID
 
 


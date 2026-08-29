create   view [dbo].[View_BuyBalance]
AS
SELECT 
    BuyBalance.BuyBalanceID, 
    BuyBalance.UserID, 
    BuyBalance.SupplierID, 
    BuyBalance.StoreBalanceID, 
    BuyBalance.BoxID, 
    BuyBalance.BoundNumber, 
    BuyBalance.DateCreate, 
    BuyBalance.DateModify, 
    BuyBalance.BuyBalanceState, 
    BuyBalance.AsyncState, 
    BuyBalance.AsyncID,
    Users.UserName AS UserName,
    Suppliers.SupplierName AS SupplierName,
    Suppliers.CityID AS CityID,
    Cities.CityName AS CityName,
    StoreBalance.StoreBalanceName AS StoreBalanceName,
    Boxes.BoxName AS BoxName,
    ISNULL(SUM(View_SelectBuyBalance.BalanceCostDenar), 0) AS TotalAmountDenar,
    ISNULL(SUM(WithdrawalFromBox.Amount * 1448), 0) AS AmountSpentDenar,
    ISNULL(COUNT(View_SelectBuyBalance.SelectBuyBalanceID), 0) AS NumberOfItemsBuys,
    STUFF((
        SELECT 
            ' ( ' + BalanceName + ' ) '
        FROM 
            View_SelectBuyBalance AS InnerView
        WHERE 
            InnerView.BuyBalanceID = BuyBalance.BuyBalanceID
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS BalanceNames
FROM 
    dbo.BuyBalance
LEFT JOIN 
    dbo.Users ON BuyBalance.UserID = Users.UserID
LEFT JOIN 
    dbo.Suppliers ON BuyBalance.SupplierID = Suppliers.SupplierID
LEFT JOIN 
    dbo.Cities ON Suppliers.CityID = Cities.CityID
LEFT JOIN 
    dbo.StoreBalance ON BuyBalance.StoreBalanceID = StoreBalance.StoreBalanceID
LEFT JOIN 
    dbo.Boxes ON BuyBalance.BoxID = Boxes.BoxID
LEFT JOIN 
    dbo.View_SelectBuyBalance ON BuyBalance.BuyBalanceID = View_SelectBuyBalance.BuyBalanceID
LEFT JOIN 
    dbo.WithdrawalFromBox ON BuyBalance.BuyBalanceID = WithdrawalFromBox.BuyBalanceID
GROUP BY 
    BuyBalance.BuyBalanceID, 
    BuyBalance.UserID, 
    BuyBalance.SupplierID, 
    BuyBalance.StoreBalanceID, 
    BuyBalance.BoxID, 
    BuyBalance.BoundNumber, 
    BuyBalance.DateCreate, 
    BuyBalance.DateModify, 
    BuyBalance.BuyBalanceState, 
    BuyBalance.AsyncState, 
    BuyBalance.AsyncID,
    Users.UserName, 
    Suppliers.SupplierName, 
    Suppliers.CityID, 
    Cities.CityName, 
    StoreBalance.StoreBalanceName, 
    Boxes.BoxName;



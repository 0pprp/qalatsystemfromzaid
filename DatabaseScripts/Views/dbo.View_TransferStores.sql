create   view [dbo].[View_TransferStores]
AS
SELECT        TS.TransferStoreID, TS.FromStoreID, TS.ToStoreID, TS.UserID, TS.TransferStoreDate, TS.State, TS.AsyncState, TS.AsyncID, U.UserName, FS.StoreName AS FromStoreName, TSX.StoreName AS ToStoreName,
                             (SELECT        STRING_AGG(' ( ' + ItemName + ' ( ' + CAST(Quantity AS NVARCHAR(255)) + ' ) ' + ' ) ', ', ') AS Expr1
                               FROM            dbo.View_SelectItemsTransferStores
                               WHERE        (TransferStoreID = TS.TransferStoreID)) AS ItemsNames, ISNULL(SUM(VSITS.Quantity), 0) AS TotalQuantity
FROM            dbo.TransferStores AS TS LEFT OUTER JOIN
                         dbo.Users AS U ON TS.UserID = U.UserID LEFT OUTER JOIN
                         dbo.Stores AS FS ON TS.FromStoreID = FS.StoreID LEFT OUTER JOIN
                         dbo.Stores AS TSX ON TS.ToStoreID = TSX.StoreID LEFT OUTER JOIN
                         dbo.View_SelectItemsTransferStores AS VSITS ON TS.TransferStoreID = VSITS.TransferStoreID
GROUP BY TS.TransferStoreID, TS.FromStoreID, TS.ToStoreID, TS.UserID, TS.TransferStoreDate, TS.State, TS.AsyncState, TS.AsyncID, U.UserName, FS.StoreName, TSX.StoreName


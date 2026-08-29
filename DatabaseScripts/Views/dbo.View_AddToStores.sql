create   view [dbo].[View_AddToStores]
AS
SELECT 
    AddToStores.*, 
    Users.UserName AS UserName,
    Stores.StoreName AS StoreName,
    (
        SELECT 
            STUFF((
                SELECT 
                    ' ( ' + ItemName + ' ( ' + CAST(Quantity AS nvarchar(255)) + ' ) ' + ' ) '
                FROM 
                    dbo.View_SelectItemsAddToStores AS vsi
                WHERE 
                    vsi.AddToStoreID = AddToStores.AddToStoreID
                FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '')
    ) AS ItemsNames,
    (
        SELECT 
            SUM(Quantity) 
        FROM 
            dbo.View_SelectItemsAddToStores AS vsi
        WHERE 
            vsi.AddToStoreID = AddToStores.AddToStoreID
    ) AS Quantity
FROM 
    dbo.AddToStores
LEFT JOIN 
    dbo.Users ON AddToStores.UserID = Users.UserID
LEFT JOIN 
    dbo.Stores ON AddToStores.StoreID = Stores.StoreID;



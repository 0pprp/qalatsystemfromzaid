 
create view [dbo].[View_CustomersTempItemsNames]
as

SELECT 
    c.CustomerID,
    c.CustomerName,
    (
        SELECT 
            STRING_AGG(CONCAT(si.ItemName, ' (', si.Quantity, ')'), ' + ')
        FROM 
            View_SelectItemsSalesItemsNames si
        WHERE 
            si.CustomerID = c.CustomerID
    ) AS ItemsNames
FROM 
    Customers c;


CREATE proc [dbo].[DeleteSuppliersAccountsAsyncID]
@BuyID int = NULL
as

DECLARE @AsyncID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT AsyncID
FROM SuppliersAccounts  where BuyID=@BuyID;;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @AsyncID;
WHILE @@FETCH_STATUS = 0
BEGIN
    Insert into DeleteData (SuppliersAccountsAsyncID) values (@AsyncID)
    FETCH NEXT FROM cursor_name INTO @AsyncID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


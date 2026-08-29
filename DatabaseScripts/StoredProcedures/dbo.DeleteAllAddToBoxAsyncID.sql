CREATE proc [dbo].[DeleteAllAddToBoxAsyncID]
@CustomerPaymentID int = NULL
as

DECLARE @AsyncID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT AsyncID
FROM AddToBox  where CustomerPaymentID=@CustomerPaymentID;;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @AsyncID;
WHILE @@FETCH_STATUS = 0
BEGIN
    Insert into DeleteData (AddToBoxAsyncID) values (@AsyncID)
    FETCH NEXT FROM cursor_name INTO @AsyncID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


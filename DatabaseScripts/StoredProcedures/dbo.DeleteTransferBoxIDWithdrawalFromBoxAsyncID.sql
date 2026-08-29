
CREATE proc [dbo].[DeleteTransferBoxIDWithdrawalFromBoxAsyncID]
@TransferBoxID int = NULL
as

DECLARE @AsyncID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT AsyncID
FROM WithdrawalFromBox  where TransferBoxID=@TransferBoxID;;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @AsyncID;
WHILE @@FETCH_STATUS = 0
BEGIN
    Insert into DeleteData (WithdrawalFromBoxAsyncID) values (@AsyncID)
    FETCH NEXT FROM cursor_name INTO @AsyncID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;



CREATE proc [dbo].[DeleteSelectItemsSalesAsyncID]
@CustomerSaleID int  = NULL
as

DECLARE @AsyncID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT AsyncID
FROM SelectItemsSales  where CustomerSaleID=@CustomerSaleID;;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @AsyncID;
WHILE @@FETCH_STATUS = 0
BEGIN
    Insert into DeleteData (SelectItemsSalesAsyncID) values (@AsyncID)
    FETCH NEXT FROM cursor_name INTO @AsyncID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


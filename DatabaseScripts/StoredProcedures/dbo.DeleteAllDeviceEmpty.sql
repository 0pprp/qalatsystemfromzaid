CREATE proc [dbo].[DeleteAllDeviceEmpty]
as
declare @UserID int = (select top 1 UserID from Users order by UserID desc)
DECLARE @CustomerSaleID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT CustomerSaleID
FROM View_CustomersSales  where AmountTotalSalesDenar=0 and AmountTotalCostDenar = 0 and AmountTotalDayDenar=0;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @CustomerSaleID;
WHILE @@FETCH_STATUS = 0
BEGIN
    exec DeleteCustomersSales @CustomerSaleID = @CustomerSaleID , @UserID = @UserID
    FETCH NEXT FROM cursor_name INTO @CustomerSaleID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;



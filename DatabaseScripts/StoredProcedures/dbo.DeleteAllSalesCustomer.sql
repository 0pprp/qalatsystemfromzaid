CREATE proc [dbo].[DeleteAllSalesCustomer]
@CustomerID int,
@UserID int
as
DECLARE @CustomerSaleID int;
DECLARE cursor_name CURSOR FOR
SELECT CustomerSaleID
FROM CustomersSales  where  CustomerID=@CustomerID;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @CustomerSaleID;
WHILE @@FETCH_STATUS = 0
BEGIN
    exec DeleteCustomersSales @CustomerSaleID=@CustomerSaleID, @UserID=@UserID
    FETCH NEXT FROM cursor_name INTO @CustomerSaleID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;



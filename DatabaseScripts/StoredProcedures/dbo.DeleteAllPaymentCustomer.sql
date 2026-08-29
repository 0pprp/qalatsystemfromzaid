CREATE proc [dbo].[DeleteAllPaymentCustomer]
@CustomerID int,
@UserID int
as
DECLARE @CustomerPaymentID int;
DECLARE cursor_name CURSOR FOR
SELECT CustomerPaymentID
FROM CustomersPayments  where CustomerID=@CustomerID;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @CustomerPaymentID;
WHILE @@FETCH_STATUS = 0
BEGIN
    exec DeleteCustomersPayments @CustomerPaymentID=@CustomerPaymentID , @UserID=@UserID
    FETCH NEXT FROM cursor_name INTO @CustomerPaymentID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


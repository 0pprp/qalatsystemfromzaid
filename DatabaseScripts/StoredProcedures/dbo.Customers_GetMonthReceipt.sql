CREATE PROC [dbo].[Customers_GetMonthReceipt]
    @DelegateID INT = NULL,
    @ShowType NVARCHAR(50) = NULL
AS
BEGIN
    IF @ShowType = N'الجميع'
    BEGIN
        SELECT * 
        FROM View_CustomerMonthPaymentDevice
        WHERE 
            (@DelegateID IS NULL OR DelegateID = @DelegateID)
    END

    IF @ShowType = N'المسددين'
    BEGIN
        SELECT * 
        FROM View_CustomerMonthPaymentDevice
        WHERE 
            (@DelegateID IS NULL OR DelegateID = @DelegateID) AND
            (
                Amount1 > 0 OR Amount2 > 0 OR Amount3 > 0 OR Amount4 > 0 OR Amount5 > 0 OR
                Amount6 > 0 OR Amount7 > 0 OR Amount8 > 0 OR Amount9 > 0 OR Amount10 > 0 OR
                Amount11 > 0 OR Amount12 > 0 OR Amount13 > 0 OR Amount14 > 0 OR Amount15 > 0 OR
                Amount16 > 0 OR Amount17 > 0 OR Amount18 > 0 OR Amount19 > 0 OR Amount20 > 0 OR
                Amount21 > 0 OR Amount22 > 0 OR Amount23 > 0 OR Amount24 > 0 OR Amount25 > 0 OR
                Amount26 > 0 OR Amount27 > 0 OR Amount28 > 0 OR Amount29 > 0 OR Amount30 > 0
            ) AND
            AmountRemaining > 0
    END

    IF @ShowType = N'المتوقفين'
    BEGIN
        SELECT * 
        FROM View_CustomerMonthPaymentDevice
        WHERE 
            (@DelegateID IS NULL OR DelegateID = @DelegateID) AND
            (
                Amount1 = 0 AND Amount2 = 0 AND Amount3 = 0 AND Amount4 = 0 AND Amount5 = 0 AND
                Amount6 = 0 AND Amount7 = 0 AND Amount8 = 0 AND Amount9 = 0 AND Amount10 = 0 AND
                Amount11 = 0 AND Amount12 = 0 AND Amount13 = 0 AND Amount14 = 0 AND Amount15 = 0 AND
                Amount16 = 0 AND Amount17 = 0 AND Amount18 = 0 AND Amount19 = 0 AND Amount20 = 0 AND
                Amount21 = 0 AND Amount22 = 0 AND Amount23 = 0 AND Amount24 = 0 AND Amount25 = 0 AND
                Amount26 = 0 AND Amount27 = 0 AND Amount28 = 0 AND Amount29 = 0 AND Amount30 = 0
            ) AND
            AmountRemaining > 0
    END
END


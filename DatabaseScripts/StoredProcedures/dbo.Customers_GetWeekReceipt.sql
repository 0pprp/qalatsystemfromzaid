 
CREATE PROC [dbo].[Customers_GetWeekReceipt]  
    @DelegateID INT = NULL,
    @ShowType NVARCHAR(50) = NULL
AS
BEGIN
    IF @ShowType = N'الجميع'
    BEGIN
        SELECT * 
        FROM View_CustomerWeekPaymentDevice
        WHERE 
            (@DelegateID IS NULL OR DelegateID = @DelegateID)
            AND ISNULL(IsFakeSale, 0) = 0
    END

    IF @ShowType = N'المسددين'
    BEGIN
        SELECT * 
        FROM View_CustomerWeekPaymentDevice
        WHERE 
            (@DelegateID IS NULL OR DelegateID = @DelegateID) AND
            (
                Amount1 > 0 OR
                Amount2 > 0 OR
                Amount3 > 0 OR
                Amount4 > 0 OR
                Amount5 > 0 OR
                Amount6 > 0 OR
                Amount7 > 0
            ) AND
            AmountRemaining > 0
            AND ISNULL(IsFakeSale, 0) = 0
    END

    IF @ShowType = N'المتوقفين'
    BEGIN
        SELECT * 
        FROM View_CustomerWeekPaymentDevice
        WHERE 
            (@DelegateID IS NULL OR DelegateID = @DelegateID) AND
            (
                Amount1 = 0 AND
                Amount2 = 0 AND
                Amount3 = 0 AND
                Amount4 = 0 AND
                Amount5 = 0 AND
                Amount6 = 0 AND
                Amount7 = 0
            ) AND
            AmountRemaining > 0
            AND ISNULL(IsFakeSale, 0) = 0
    END
END


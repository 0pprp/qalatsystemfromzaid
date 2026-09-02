CREATE OR ALTER PROC [dbo].[Customers_PostWeekDecision]
    @CustomerID INT,
    @UserID INT,
    @DecisionType NVARCHAR(50),
    @Note NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DecisionType NOT IN (N'متواصل', N'قانونية', N'وهمي')
    BEGIN
        RAISERROR(N'نوع القرار غير صالح', 16, 1);
        RETURN;
    END

    DECLARE @WeekPaid FLOAT = 0;
    DECLARE @AmountTotalSales FLOAT = 0;
    DECLARE @PaidPercent FLOAT = 0;
    DECLARE @CustomerName NVARCHAR(255) = N'';
    DECLARE @SnoozeUntil DATETIME = NULL;
    DECLARE @WeekStartDate DATE = CAST(DATEADD(DAY, -7, GETDATE()) AS DATE);
    DECLARE @WeekEndDate DATE = CAST(DATEADD(DAY, -1, GETDATE()) AS DATE);
    DECLARE @DecisionID INT;

    SELECT
        @WeekPaid = ROUND(SUM(ISNULL(AmountDenar, 0)), -3)
    FROM View_ReceiptCustomerDate
    WHERE CustomerID = @CustomerID
      AND CAST(PaymentDate AS DATE) >= @WeekStartDate
      AND CAST(PaymentDate AS DATE) <= @WeekEndDate;

    SET @WeekPaid = ISNULL(@WeekPaid, 0);

    SELECT
        @AmountTotalSales = ISNULL(AmountTotalSales, 0),
        @CustomerName = ISNULL(CustomerName, N'')
    FROM View_CustomerWeekPaymentDevice
    WHERE CustomerID = @CustomerID;

    IF @CustomerName = N''
    BEGIN
        SELECT @CustomerName = ISNULL(CustomerName, N'') FROM Customers WHERE CustomerID = @CustomerID;
    END

    IF @AmountTotalSales > 0
        SET @PaidPercent = ROUND((@WeekPaid * 100.0) / @AmountTotalSales, 2);

    IF @DecisionType = N'متواصل'
        SET @SnoozeUntil = DATEADD(DAY, 7, GETDATE());

    INSERT INTO CustomerWeekDecisions
        (CustomerID, UserID, DecisionType, WeekPaid, AmountTotalSales, PaidPercent,
         WeekStartDate, WeekEndDate, SnoozeUntil, Note, CreatedDate)
    VALUES
        (@CustomerID, @UserID, @DecisionType, @WeekPaid, @AmountTotalSales, @PaidPercent,
         @WeekStartDate, @WeekEndDate, @SnoozeUntil, @Note, GETDATE());

    SET @DecisionID = SCOPE_IDENTITY();

    IF @DecisionType = N'قانونية'
    BEGIN
        UPDATE Customers SET IsLegal = 1, UpdatedDate = GETDATE() WHERE CustomerID = @CustomerID;
        IF OBJECT_ID(N'dbo.CustomerWeekPaymentSnapshot', N'U') IS NOT NULL
            UPDATE CustomerWeekPaymentSnapshot SET IsLegal = 1 WHERE CustomerID = @CustomerID;
    END
    ELSE IF @DecisionType = N'وهمي'
    BEGIN
        UPDATE Customers SET IsFakeSale = 1, UpdatedDate = GETDATE() WHERE CustomerID = @CustomerID;
        IF OBJECT_ID(N'dbo.CustomerWeekPaymentSnapshot', N'U') IS NOT NULL
            UPDATE CustomerWeekPaymentSnapshot SET IsFakeSale = 1 WHERE CustomerID = @CustomerID;
    END

    INSERT INTO DecisionNotifications (DecisionID, IsRead, CreatedDate)
    VALUES (@DecisionID, 0, GETDATE());

    IF OBJECT_ID(N'dbo.Activities', N'U') IS NOT NULL
    BEGIN
        INSERT INTO Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
        VALUES (
            @UserID,
            N'قرار أسبوعي (' + @DecisionType + N') للزبون ' + @CustomerName,
            GETUTCDATE(),
            'false',
            NEWID()
        );
    END

    SELECT
        D.DecisionID,
        D.CustomerID,
        D.UserID,
        D.DecisionType,
        D.WeekPaid,
        D.AmountTotalSales,
        D.PaidPercent,
        D.WeekStartDate,
        D.WeekEndDate,
        D.SnoozeUntil,
        D.Note,
        D.CreatedDate,
        C.CustomerName,
        C.PhoneNumber,
        U.UserName,
        U.UserType,
        ISNULL(C.IsLegal, 0) AS IsLegal,
        ISNULL(C.IsFakeSale, 0) AS IsFakeSale
    FROM CustomerWeekDecisions D
    INNER JOIN Customers C ON C.CustomerID = D.CustomerID
    INNER JOIN Users U ON U.UserID = D.UserID
    WHERE D.DecisionID = @DecisionID;
END

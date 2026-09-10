SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Exclude auto-accepted collection payments from manual accountant approval queue.
-- Harden Approve to ignore AutoPostEnabled=1 (HostedService / catch-up posts them).
-- Safe to re-run.

IF OBJECT_ID('dbo.CustomersPaymentsRequest_GetAll', 'P') IS NOT NULL
    DROP PROCEDURE dbo.CustomersPaymentsRequest_GetAll;
GO

CREATE PROC [dbo].[CustomersPaymentsRequest_GetAll]
    @CustomerName NVARCHAR(100) = NULL,
    @DelegateID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT v.*
    FROM dbo.View_CustomersPaymentsRequestFinal AS v
    WHERE
        (@CustomerName IS NULL OR v.CustomerName LIKE N'%' + @CustomerName + N'%')
        AND (@DelegateID IS NULL OR v.DelegateID = @DelegateID)
        AND NOT EXISTS (
            SELECT 1
            FROM dbo.CustomersPaymentsRequest AS cpr
            WHERE cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
              AND ISNULL(cpr.AutoPostEnabled, 0) = 1
        );
END
GO

IF OBJECT_ID('dbo.CustomersPaymentsRequest_Approve', 'P') IS NOT NULL
    DROP PROCEDURE dbo.CustomersPaymentsRequest_Approve;
GO

CREATE PROCEDURE [dbo].[CustomersPaymentsRequest_Approve]
    @CustomersPaymentsRequestID INT,
    @UserCreateID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @CustomerID INT,
        @DateCreate DATETIME,
        @Amount FLOAT,
        @AmountHash FLOAT,
        @AmountRemaining FLOAT,
        @AutoPost BIT;

    SELECT
        @CustomerID = cpr.CustomerID,
        @DateCreate = cpr.PaymentDate,
        @Amount = cpr.Amount,
        @AmountHash = cpr.Amount / 1448.0,
        @AmountRemaining = v.AmountRemaining,
        @AutoPost = ISNULL(cpr.AutoPostEnabled, 0)
    FROM dbo.CustomersPaymentsRequest AS cpr
    JOIN dbo.View_CustomersPaymentsRequestFinal AS v
        ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
    WHERE cpr.CustomersPaymentsRequestID = @CustomersPaymentsRequestID;

    IF @AutoPost = 1
        RETURN;

    IF (@AmountRemaining > 0 AND @AmountRemaining >= @Amount)
    BEGIN
        EXEC dbo.CustomersPayments_Create
            @UserID = @UserCreateID,
            @CustomerID = @CustomerID,
            @DateCreate = @DateCreate,
            @Amount = @AmountHash;

        DELETE FROM dbo.CustomersPaymentsRequest
        WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;
    END;
END;
GO

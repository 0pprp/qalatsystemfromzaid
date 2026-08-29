

CREATE procEDURE [dbo].[InsertServerCustomersPayments]
    @UserID INT = NULL,
    @CustomerID INT = NULL,
    @BoxID INT = NULL,
    @PaymentDate DATETIME= NULL,
    @BoundNumber INT = NULL,
    @DelegateID INT = NULL,
    @AccountZero BIT = NULL,
    @DelegateState BIT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @SelectState BIT = NULL,
    @Location NVARCHAR(255)= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[CustomersPayments]
           ([UserID]
           ,[CustomerID]
           ,[BoxID]
           ,[PaymentDate]
           ,[BoundNumber]
           ,[DelegateID]
           ,[AccountZero]
           ,[DelegateState]
           ,[AsyncState]
           ,[AsyncID]
           ,[SelectState]
           ,[Location])
     VALUES
           (@UserID
           ,@CustomerID
           ,@BoxID
           ,@PaymentDate
           ,@BoundNumber
           ,@DelegateID
           ,@AccountZero
           ,@DelegateState
           ,@AsyncState
           ,@AsyncID
           ,@SelectState
           ,@Location)
END


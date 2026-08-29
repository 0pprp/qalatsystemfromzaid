

CREATE procEDURE [dbo].[InsertServerExchangeItemsDebts]
    @UserID INT = NULL,
    @ExchangeItemID INT = NULL,
    @AmountDebt FLOAT= NULL,
    @DateDebt DATETIME= NULL,
    @Purpose NVARCHAR(MAX)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @AccountType NVARCHAR(MAX)= NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @AsyncState BIT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[ExchangeItemsDebts]
           ([UserID]
           ,[ExchangeItemID]
           ,[AmountDebt]
           ,[DateDebt]
           ,[Purpose]
           ,[Notes]
           ,[AccountType]
           ,[AsyncID]
           ,[AsyncState])
     VALUES
           (@UserID
           ,@ExchangeItemID
           ,@AmountDebt
           ,@DateDebt
           ,@Purpose
           ,@Notes
           ,@AccountType
           ,@AsyncID
           ,@AsyncState)
END


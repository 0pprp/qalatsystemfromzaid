

CREATE procEDURE [dbo].[InsertServerExchangeItems]
    @UserID INT = NULL,
    @CityID INT = NULL,
    @ExchangeItemName NVARCHAR(255)= NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @LimitAmount FLOAT= NULL,
    @ExchangeItemsState BIT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[ExchangeItems]
           ([UserID]
           ,[CityID]
           ,[ExchangeItemName]
           ,[AsyncState]
           ,[AsyncID]
           ,[LimitAmount]
           ,[ExchangeItemsState])
     VALUES
           (@UserID
           ,@CityID
           ,@ExchangeItemName
           ,@AsyncState
           ,@AsyncID
           ,@LimitAmount
           ,@ExchangeItemsState)
END



CREATE PROCEDURE [dbo].[ExchangesItems_Update]
    @ExchangeItemID INT,
    @ExchangeItemName NVARCHAR(100),
    @LimitAmount FLOAT,
    @UserUpdateID INT
AS
BEGIN
    UPDATE ExchangeItems
    SET 
        ExchangeItemName = @ExchangeItemName,
        LimitAmount = @LimitAmount 
    WHERE ExchangeItemID = @ExchangeItemID;
	    			INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل بند الصرف  '+@ExchangeItemName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

    SELECT * FROM View_ExchangeItems WHERE ExchangeItemID = @ExchangeItemID;
END;



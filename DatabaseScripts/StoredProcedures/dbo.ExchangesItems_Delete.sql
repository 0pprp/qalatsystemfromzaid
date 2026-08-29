
CREATE PROCEDURE [dbo].[ExchangesItems_Delete]
    @ExchangeItemID INT,
    @UserDeleteID INT
AS
BEGIN
    UPDATE ExchangeItems
    SET 
        ExchangeItemsState = 0  
    WHERE ExchangeItemID = @ExchangeItemID 

		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف بند الصرف '+(select ExchangeItemName from ExchangeItems where   ExchangeItemID=@ExchangeItemID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

END;



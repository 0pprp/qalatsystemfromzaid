
CREATE PROCEDURE [dbo].[Items_Delete]
@ItemID INT,
@UserDeleteID INT
AS
UPDATE Items
SET ItemState = 0 
WHERE ItemID = @ItemID;
		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف العنصر  '+(select ItemName from Items where    ItemID=@ItemID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() ) 



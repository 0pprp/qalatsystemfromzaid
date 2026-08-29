
CREATE PROCEDURE [dbo].[Items_Update]
@ItemID INT,
@StoreID INT,
@UserUpdateID INT,
@ItemName NVARCHAR(255),
@ItemPriceDenar float,
@ItemCostDenar float,
@AmountDayDenar float,
@Quantity INT,
@NotificationNumber INT,
@Notes NVARCHAR(MAX)
AS
UPDATE Items
SET StoreID = @StoreID,
ItemName = @ItemName,
ItemPrice = @ItemPriceDenar/1448,
ItemCost = @ItemCostDenar/1448,
AmountDay = @AmountDayDenar/1448,
Quantity = @Quantity,
NotificationNumber = @NotificationNumber,
Notes = @Notes
WHERE ItemID = @ItemID;

	    			INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل العنصر  '+@ItemName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

select * from View_Items where ItemID=@ItemID 



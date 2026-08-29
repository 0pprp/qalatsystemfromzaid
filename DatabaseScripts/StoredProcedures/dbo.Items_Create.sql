CREATE PROCEDURE [dbo].[Items_Create]
    @StoreID INT,
    @UserCreateID INT,
    @ItemName NVARCHAR(255),
    @ItemPriceDenar float,
    @ItemCostDenar float,
    @AmountDayDenar float,
    @Quantity INT,
    @NotificationNumber INT,
    @Notes NVARCHAR(MAX)
AS
INSERT INTO Items (StoreID, UserID, ItemName, ItemPrice, ItemCost, AmountDay, Quantity, NotificationNumber, Notes, AsyncID,AsyncState)
VALUES (@StoreID, @UserCreateID, @ItemName, @ItemPriceDenar/1448, @ItemCostDenar/1448, @AmountDayDenar/1448, @Quantity, @NotificationNumber, @Notes, NEWID(),0);

	INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserCreateID
           ,N'تم اضافة المادة  '+@ItemName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

DECLARE @LastId int;
SET @LastId = IDENT_CURRENT('Items');
select * from View_Items where ItemID=@LastId 


CREATE proc [dbo].[UpdateItemNoImage]
@ItemID int = NULL,
@StoreID int = NULL,
@UserID int = NULL,
@ItemName nvarchar(255),
@ItemPrice float,
@ItemCost float,
@Quantity int = NULL,
@Notes nvarchar(max) = NULL,
@NotificationNumber int = NULL,
@AmountDay float 
as
 
 
UPDATE [dbo].[Items]
   SET [StoreID] = @StoreID 
      ,[UserID] = @UserID 
      ,[ItemName] = @ItemName 
      ,[ItemPrice] = @ItemPrice 
      ,[ItemCost] = @ItemCost 
      ,[Quantity] = @Quantity 
      ,[Notes] = @Notes 
      ,[NotificationNumber] = @NotificationNumber 
      ,[AmountDay] = @AmountDay 
 WHERE  ItemID=@ItemID
 


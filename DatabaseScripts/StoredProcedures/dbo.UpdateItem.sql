CREATE proc [dbo].[UpdateItem]
@ItemID int = NULL,
@StoreID int = NULL,
@UserID int = NULL,
@ItemName nvarchar(255),
@ItemPrice float,
@ItemCost float,
@Quantity int = NULL,
@Notes nvarchar(max),
@NotificationNumber int = NULL,
@AmountDay float 
as
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل بيانات العنصر من '+(select ItemName from Items where ItemID=@ItemID)+N' الى '+@ItemName+N' وسعر البيع من '+convert(nvarchar(255),(select ItemPrice*1448 from Items where ItemID=@ItemID))+N' الى '+convert(nvarchar(255),@ItemPrice*1448)+N' وسعر الشراء من '+convert(nvarchar(255),(select ItemCost*1448 from Items where ItemID=@ItemID))+N' الى '+convert(nvarchar(255),@ItemCost*1448)+N' والقسط اليومي من '+convert(nvarchar(255),(select AmountDay*1448 from Items where ItemID=@ItemID))+N' الى '+convert(nvarchar(255),@AmountDay*1448)+N'  و المخزن من '+(select StoreName from View_Items where ItemID=@ItemID)+N' الى '+(select StoreName from Stores where StoreID=@StoreID)+N' و عد التنبية من '+convert(nvarchar(255),(select NotificationNumber from Items where ItemID=@ItemID))+N' الى '+convert(nvarchar(255),@NotificationNumber)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
 
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
 


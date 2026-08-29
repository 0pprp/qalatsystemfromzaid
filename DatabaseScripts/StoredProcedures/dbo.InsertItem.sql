CREATE proc [dbo].[InsertItem]
@StoreID int = NULL,
@UserID int = NULL,
@ItemName nvarchar(255) = NULL,
@ItemPrice float = NULL,
@ItemCost float = NULL,
@Quantity int = NULL,
@Notes nvarchar(max) = NULL,
@NotificationNumber int = NULL,
@AmountDay float = NULL
as
 
INSERT INTO [dbo].[Items]
           ([StoreID]
           ,[UserID]
           ,[ItemName]
           ,[ItemPrice]
           ,[ItemCost]
           ,[Quantity]
           ,[Notes]
           ,[NotificationNumber]
           ,[AmountDay]
           ,[AsyncState]
           ,[ItemState]
		   ,[AsyncID])
     VALUES
           (@StoreID 
           ,@UserID 
           ,@ItemName 
           ,@ItemPrice 
           ,@ItemCost 
           ,@Quantity 
           ,@Notes 
           ,@NotificationNumber 
           ,@AmountDay 
           ,'false'
           ,'true'
		   ,NEWID())
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة العنصر '+@ItemName+N' الى المخزن '+(select StoreName from Stores where StoreID=@StoreID)+N' بسعر البيع '+convert(nvarchar(255),@ItemPrice*1448)+N' و سعر الشراء '+convert(nvarchar(255),@ItemCost*1448)+N' و القسط اليومي '+convert(nvarchar(255),@AmountDay*1448)+N' و الكمية '+convert(nvarchar(255),@Quantity)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


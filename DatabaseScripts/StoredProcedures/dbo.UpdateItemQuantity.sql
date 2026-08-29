CREATE proc [dbo].[UpdateItemQuantity]
@ItemID int = NULL,
@Quantity int = NULL,
@UserID int = NULL
as

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تغيير كمية العنصر '+(select ItemName from Items where ItemID=@ItemID)+N' من '+(select Quantity from Items where ItemID=@ItemID)+N' الى '+(select Quantity+@Quantity from Items where ItemID=@ItemID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Items set Quantity=Quantity+@Quantity where ItemID=@ItemID


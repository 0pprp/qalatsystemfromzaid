CREATE proc [dbo].[DeleteItem]
@ItemID int = NULL,
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
           ,N'تم حذف العنصر '+(select ItemName from View_Items where ItemID=@ItemID)+N' من المخزن '+(select StoreName from View_Items where ItemID=@ItemID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

update Items set ItemState='false'
where ItemID=@ItemID


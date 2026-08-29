CREATE proc [dbo].[DeleteBox]
@BoxID int = NULL,
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
           ,N'تم حذف الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Boxes set BoxState='false' where BoxID=@BoxID
 
 



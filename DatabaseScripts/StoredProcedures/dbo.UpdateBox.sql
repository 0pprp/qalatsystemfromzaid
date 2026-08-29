CREATE proc [dbo].[UpdateBox]
@BoxID int = NULL,
@BoxName nvarchar(255) ,
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
           ,N'تم تعديل الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+N' الى الخزينة '+@BoxName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Boxes set BoxName=@BoxName where BoxID=@BoxID
 
 



CREATE proc [dbo].[AddToBox_Create]
@BoxID int = NULL,
@Amount float = NULL,
@Notes nvarchar(max)= NULL,
@UserID int = NULL 
as
INSERT INTO [dbo].[AddToBox]
           ([BoxID]
           ,[Amount]
           ,[Notes]
           ,[UserID]
		   ,DateCreate
		   ,AsyncState
		   ,AsyncID)
     VALUES
           (@BoxID 
           ,@Amount 
           ,@Notes 
           ,@UserID 
		   ,getdate()
           ,'false'
           ,newID() )

			
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة المبلغ '+(CONVERT(nvarchar(100),@Amount*1448))+N' الى الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


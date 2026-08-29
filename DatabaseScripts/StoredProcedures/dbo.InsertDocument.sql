CREATE proc [dbo].[InsertDocument]
@UserID int = NULL,
@FromAccount nvarchar(255) = NULL,
@ToAccount nvarchar(255) = NULL,
@DocumentDateCreate datetime = NULL,
@Notes nvarchar(max) = NULL,
@DocumentType nvarchar(255) = NULL,
@Amount float = NULL
as
 
INSERT INTO [dbo].[Documents]
           ([UserID]
           ,[FromAccount]
           ,[ToAccount]
           ,[DocumentDateCreate]
           ,[Notes]
           ,[DocumentType]
           ,[Amount]
           ,[DocumentState]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@FromAccount 
           ,@ToAccount 
           ,@DocumentDateCreate 
           ,@Notes 
           ,@DocumentType 
           ,@Amount 
           ,'true' 
           ,'false'
		   ,NEWID())
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة السند '+@DocumentType+N' بمبلغ '+convert(nvarchar(255),@Amount*1448)+N' من حساب '+@FromAccount+N' الى حساب '+@ToAccount+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


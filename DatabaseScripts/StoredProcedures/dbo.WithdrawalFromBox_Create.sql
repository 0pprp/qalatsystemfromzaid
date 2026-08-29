create proc [dbo].[WithdrawalFromBox_Create]
@BoxID int = NULL,
@Amount float= NULL,
@Notes nvarchar(max)= NULL,
@UserID int = NULL
as
 
INSERT INTO [dbo].[WithdrawalFromBox]
           ([BoxID]
           ,[Amount]
           ,[Purpose]
           ,[Notes]
           ,[UserID]
           ,[DateCreate]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@BoxID 
           ,@Amount 
           ,@Notes 
           ,@Notes 
           ,@UserID 
           ,getdate() 
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
           ,N'تم سحب المبلغ '+CONVERT(nvarchar(255),@Amount*1448)+N' من الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


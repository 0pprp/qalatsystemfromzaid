CREATE proc [dbo].[InsertWithdrawalFromBox]
@BoxID int = NULL,
@Amount float= NULL,
@Purpose nvarchar(max)= NULL,
@Notes nvarchar(max)= NULL,
@UserID int = NULL,
@DateCreate datetime = NULL
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
           ,@Purpose 
           ,@Notes 
           ,@UserID 
           ,@DateCreate 
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
 


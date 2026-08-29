CREATE proc [dbo].[InsertTransferBoxs]  
@FromBoxID int = NULL,
@ToBoxID int = NULL,
@UserID int = NULL,
@Amount float= NULL,
@Notes nvarchar(max)= NULL,
@DateCreate datetime = NULL
as
 
INSERT INTO [dbo].[TransferBoxs]
           ([FromBoxID]
           ,[ToBoxID]
           ,[UserID]
           ,[Amount]
           ,[Notes]
           ,[DateCreate]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@FromBoxID 
           ,@ToBoxID 
           ,@UserID 
           ,@Amount 
           ,@Notes 
           ,@DateCreate 
           ,'false'
		   ,NEWID())

INSERT INTO [dbo].[WithdrawalFromBox]
           ([BoxID]
           ,[Amount]
           ,[Purpose]
           ,[Notes]
           ,[UserID]
           ,[DateCreate]
           ,[TransferBoxID]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@FromBoxID 
           ,@Amount 
           ,N'تحويل من الخزينة '+(select BoxName from Boxes where BoxID=@FromBoxID)+N' الى خزينة '+(select BoxName from Boxes where BoxID=@ToBoxID)+'' 
           ,@Notes 
           ,@UserID 
           ,@DateCreate 
		   ,(select top 1 TransferBoxID from TransferBoxs where FromBoxID=@FromBoxID and ToBoxID=@ToBoxID order by TransferBoxID desc)
           ,'false'
		   ,NEWID())
 

INSERT INTO [dbo].[AddToBox]
           ([BoxID]
           ,[Amount]
           ,[Notes]
           ,[UserID]
           ,[DateCreate]
           ,[TransferBoxID]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@ToBoxID 
           ,@Amount 
           ,@Notes 
           ,@UserID 
           ,@DateCreate 
		   ,(select top 1 TransferBoxID from TransferBoxs where FromBoxID=@FromBoxID and ToBoxID=@ToBoxID order by TransferBoxID desc)
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
           ,N'تم سحب المبلغ '+CONVERT(nvarchar(255),@Amount*1448)+N' من الخزينة '+(select BoxName from Boxes where BoxID=@FromBoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة المبلغ '+(CONVERT(nvarchar(255),@Amount*1448))+N' الى الخزينة '+(select BoxName from Boxes where BoxID=@ToBoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تحويل المبلغ '+CONVERT(nvarchar(255),@Amount*1448)+N' من الخزينة '+(select BoxName from Boxes where BoxID=@FromBoxID)+N' الى خزينة '+(select BoxName from Boxes where BoxID=@ToBoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


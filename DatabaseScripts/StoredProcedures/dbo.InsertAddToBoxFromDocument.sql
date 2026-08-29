CREATE proc [dbo].[InsertAddToBoxFromDocument]
@UserID int = NULL,
@Amount float = NULL,
@BoxID int = NULL,
@EmployeeID int = NULL,
@DateCreate datetime = NULL
as
INSERT INTO [dbo].[AddToBox]
           ([UserID]
           ,[Amount]
           ,[BoxID]
           ,[EmployeeID]
           ,[DateCreate]
		   ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@Amount 
           ,@BoxID 
           ,@EmployeeID 
           ,@DateCreate 
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
           ,N'تم اضافة المبلغ '+(CONVERT(nvarchar(255),@Amount*1448))+N' الى الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


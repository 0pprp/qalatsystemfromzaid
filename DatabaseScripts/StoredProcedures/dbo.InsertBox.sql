CREATE proc [dbo].[InsertBox]
@BoxName nvarchar(255) = NULL,
@UserID int = NULL
as
 
INSERT INTO [dbo].[Boxes]
           ([BoxName]
           ,[AsyncState]
           ,[BoxState]
		   ,[AsyncID])
     VALUES
           (@BoxName,'false','true', NEWID())
 

 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة الخزينة '+@BoxName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


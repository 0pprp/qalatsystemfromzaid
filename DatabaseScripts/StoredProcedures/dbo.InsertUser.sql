CREATE proc [dbo].[InsertUser]
@UserName nvarchar(255)= NULL,
@Email nvarchar(255)= NULL,
@Password nvarchar(255)= NULL,
@PhoneNumber nvarchar(255)= NULL,
@Address nvarchar(255)= NULL,
@UserID int = NULL,
@UserImage nvarchar(max)= NULL
as
 
INSERT INTO [dbo].[Users]
           ([UserName]
           ,[Email]
           ,[Password]
           ,[PhoneNumber]
           ,[Address]
           ,[UserState]
           ,[UserImage]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserName
           ,@Email
           ,@Password
           ,@PhoneNumber
           ,@Address
           ,'true'
           ,@UserImage
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
           ,N'تم اضافة المستخدم '+@UserName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


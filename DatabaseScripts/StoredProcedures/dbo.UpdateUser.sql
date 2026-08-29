CREATE proc [dbo].[UpdateUser]
@UserID int = NULL,
@UserName nvarchar(255),
@Email nvarchar(255),
@Password nvarchar(255),
@PhoneNumber nvarchar(255),
@Address nvarchar(255),
@UserImage nvarchar(max),
@CurrentUserID int = NULL
as
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@CurrentUserID
           ,N'تم تعديل بيانات المستخدم من '+(select UserName from Users where UserID=@UserID)+N' الى '+@UserName+N' و بريدة الالكتروني من '+(select Email from Users where UserID=@UserID)+N' الى '+@Email+N' و عنوانة من '+(select Address from Users where UserID=@UserID)+N' الى '+@Address+N' ورقم هاتفة من '+(select PhoneNumber from Users where UserID=@UserID)+N' الى '+@PhoneNumber+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
UPDATE [dbo].[Users]
   SET [UserName] = @UserName 
      ,[Email] = @Email 
      ,[Password] = @Password 
      ,[PhoneNumber] = @PhoneNumber 
      ,[Address] = @Address 
      ,[UserImage] = @UserImage 
 WHERE UserID=@UserID
 


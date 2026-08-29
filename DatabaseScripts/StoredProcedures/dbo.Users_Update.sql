
CREATE proc [dbo].[Users_Update]
@UserID int,
@UserName nvarchar(100),
@Email nvarchar(100),
@Password nvarchar(100),
@PhoneNumber nvarchar(100),
@Address nvarchar(100),
@UserUpdateID int,
@UserType nvarchar(100),
@UserImage nvarchar(max)
as
update Users set 
UserName=@UserName,
Email=@Email,
Password=@Password,
PhoneNumber=@PhoneNumber,
Address=@Address,
UserType = @UserType,
UserImage = @UserImage
where UserID=@UserID

	     INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل المستخدم  '+@UserName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

select * from Users where UserID=@UserID 



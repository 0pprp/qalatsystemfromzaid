
CREATE proc [dbo].[Users_Create]
@UserName nvarchar(100),
@Email nvarchar(100),
@Password nvarchar(100),
@PhoneNumber nvarchar(100),
@Address nvarchar(100),
@UserCreateID int,
@UserType nvarchar(100),
@UserImage nvarchar(max)
as
insert into Users (UserName,Email,Password,PhoneNumber,Address,UserState,AsyncID,AsyncState,UserType,UserImage) values (@UserName,@Email,@Password,@PhoneNumber,@Address,1,NEWID(),0,@UserType,@UserImage)
	INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserCreateID
           ,N'تم اضافة المستخدم  '+@UserName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
DECLARE @LastId int;
SET @LastId = IDENT_CURRENT('Users');
select * from Users where UserID=@LastId 


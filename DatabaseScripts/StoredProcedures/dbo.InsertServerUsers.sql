

CREATE procEDURE [dbo].[InsertServerUsers]
    @UserName NVARCHAR(255)= NULL,
    @Email NVARCHAR(255)= NULL,
    @Password NVARCHAR(255)= NULL,
    @PhoneNumber NVARCHAR(255)= NULL,
    @Address NVARCHAR(255)= NULL,
    @UserState BIT = NULL,
    @UserImage NVARCHAR(MAX)= NULL,
    @BoxID INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Users]
           ([UserName]
           ,[Email]
           ,[Password]
           ,[PhoneNumber]
           ,[Address]
           ,[UserState]
           ,[UserImage]
           ,[BoxID]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserName
           ,@Email
           ,@Password
           ,@PhoneNumber
           ,@Address
           ,@UserState
           ,@UserImage
           ,@BoxID
           ,@AsyncState
           ,@AsyncID)
END


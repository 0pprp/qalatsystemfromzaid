

CREATE procEDURE [dbo].[InsertServerEmployees]
    @UserID INT = NULL,
    @CityID INT = NULL,
    @EmployeeName NVARCHAR(255)= NULL,
    @DateOfBirth DATETIME= NULL,
    @Address NVARCHAR(255)= NULL,
    @PhoneNumber NVARCHAR(255)= NULL,
    @AcademicAchievement NVARCHAR(255)= NULL,
    @CV NVARCHAR(MAX)= NULL,
    @Attachments NVARCHAR(MAX)= NULL,
    @DateOfJoin DATETIME= NULL,
    @EmployeeImage NVARCHAR(MAX)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @EmployeeState BIT = NULL,
    @Salary FLOAT= NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Employees]
           ([UserID]
           ,[CityID]
           ,[EmployeeName]
           ,[DateOfBirth]
           ,[Address]
           ,[PhoneNumber]
           ,[AcademicAchievement]
           ,[CV]
           ,[Attachments]
           ,[DateOfJoin]
           ,[EmployeeImage]
           ,[Notes]
           ,[EmployeeState]
           ,[Salary]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@CityID
           ,@EmployeeName
           ,@DateOfBirth
           ,@Address
           ,@PhoneNumber
           ,@AcademicAchievement
           ,@CV
           ,@Attachments
           ,@DateOfJoin
           ,@EmployeeImage
           ,@Notes
           ,@EmployeeState
           ,@Salary
           ,@AsyncState
           ,@AsyncID)
END


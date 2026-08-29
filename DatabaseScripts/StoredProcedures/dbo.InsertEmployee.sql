CREATE proc [dbo].[InsertEmployee]
@UserID int = NULL,
@CityID int = NULL,
@EmployeeName nvarchar(255)= NULL,
@DateOfBirth datetime = NULL,
@Address nvarchar(255) = NULL,
@PhoneNumber nvarchar(255) = NULL,
@AcademicAchievement nvarchar(255) = NULL,
@CV nvarchar(max) = NULL,
@Attachments nvarchar(max) = NULL,
@DateOfJoin datetime = NULL,
@EmployeeImage nvarchar(max) = NULL,
@Notes nvarchar(max) = NULL,
@Salary float = NULL
as
 
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
           ,'true' 
           ,@Salary 
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
           ,N'تم اضافة الموظف '+@EmployeeName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 



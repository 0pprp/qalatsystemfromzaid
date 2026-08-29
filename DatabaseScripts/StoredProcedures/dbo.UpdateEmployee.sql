CREATE proc [dbo].[UpdateEmployee]
@EmployeeID int = NULL,
@UserID int = NULL,
@CityID int = NULL,
@EmployeeName nvarchar(255),
@DateOfBirth datetime,
@Address nvarchar(255),
@PhoneNumber nvarchar(255),
@AcademicAchievement nvarchar(255),
@CV nvarchar(max),
@Attachments nvarchar(max),
@DateOfJoin datetime,
@EmployeeImage nvarchar(max),
@Notes nvarchar(max) ,
@Salary float
as

 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل بيانات الموظف من '+(select EmployeeName from Employees where EmployeeID=@EmployeeID)+N' الى '+@EmployeeName+N' و تاريخ الميلاد من '+(select CONVERT(nvarchar(255),DateOfBirth) from Employees where EmployeeID=@EmployeeID)+N' الى '+ CONVERT(nvarchar(255),@DateOfBirth)+N' و العنوان من '+(select Address from Employees where EmployeeID=@EmployeeID)+N' الى '+@Address+N' و رقم الهاتف من '+(select PhoneNumber from Employees where EmployeeID=@EmployeeID)+N' الى '+@Address+N' و التحصيل الدراسي من '+(select AcademicAchievement from Employees where EmployeeID=@EmployeeID)+N' الى '+@AcademicAchievement+N' و تاريخ الانظمام من '+(select CONVERT(nvarchar(255),DateOfJoin) from Employees where EmployeeID=@EmployeeID)+N' الى '+CONVERT(nvarchar(255),@DateOfJoin)+N'  و الراتب من '+(select CONVERT(nvarchar(255),Salary) from Employees where EmployeeID=@EmployeeID)+N' الى '+CONVERT(nvarchar(255),@Salary)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
UPDATE [dbo].[Employees]
   SET [UserID] = @UserID 
      ,[CityID] = @CityID 
      ,[EmployeeName] = @EmployeeName 
      ,[DateOfBirth] = @DateOfBirth 
      ,[Address] = @Address 
      ,[PhoneNumber] = @PhoneNumber 
      ,[AcademicAchievement] = @AcademicAchievement 
      ,[CV] = @CV 
      ,[Attachments] = @Attachments 
      ,[DateOfJoin] = @DateOfJoin 
      ,[EmployeeImage] = @EmployeeImage 
      ,[Notes] = @Notes 
      ,[Salary] = @Salary 
 WHERE  EmployeeID=@EmployeeID


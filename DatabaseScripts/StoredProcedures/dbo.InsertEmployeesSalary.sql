CREATE proc [dbo].[InsertEmployeesSalary]
@EmployeeID int = NULL,
@UserID int = NULL,
@SalaryAmount float = NULL,
@SalaryDate datetime = NULL
as
 
INSERT INTO [dbo].[EmployeesSalaries]
           ([EmployeeID]
           ,[UserID]
           ,[SalaryAmount]
           ,[SalaryDate]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@EmployeeID 
           ,@UserID 
           ,@SalaryAmount 
           ,@SalaryDate 
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
           ,N'تم اضافة الراتب بملغ '+@SalaryAmount*1448+N' الى الموظف '+(select EmployeeName from Employees where EmployeeID=@EmployeeID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


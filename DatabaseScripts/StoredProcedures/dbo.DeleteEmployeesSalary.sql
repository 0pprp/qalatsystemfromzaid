CREATE proc [dbo].[DeleteEmployeesSalary]
@EmployeeSalaryID int = NULL,
@UserID int = NULL
as
exec DeleteDeleteEmployeesSalaryAsyncID @EmployeeSalaryID=@EmployeeSalaryID

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف الراتب الموظف '+(select EmployeeName from View_EmployeesSalaries where EmployeeSalaryID=@EmployeeSalaryID)+N' المبلغ '+(select SalaryAmountDenar from View_EmployeesSalaries where EmployeeSalaryID=@EmployeeSalaryID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from EmployeesSalaries 
where  EmployeeSalaryID=@EmployeeSalaryID


 


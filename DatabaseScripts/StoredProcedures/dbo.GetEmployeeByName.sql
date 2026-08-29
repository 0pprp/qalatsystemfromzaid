CREATE proc [dbo].[GetEmployeeByName]
@EmployeeName nvarchar(255)
as
select * from View_Employees 
where EmployeeState='true' and EmployeeName like N'%'+@EmployeeName+N'%'


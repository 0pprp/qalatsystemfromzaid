CREATE proc [dbo].[GetEmployeesSalariesByName]
@EmployeeName nvarchar(255)
as
select * from View_EmployeesSalaries 
where EmployeeName like N'%'+@EmployeeName+N'%'


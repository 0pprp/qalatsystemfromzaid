CREATE proc [dbo].[GetEmployeesSalariesByEmployee]
@EmployeeID int = NULL
as
select * from View_EmployeesSalaries
where EmployeeID=@EmployeeID 


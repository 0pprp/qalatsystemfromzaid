CREATE proc [dbo].[GetEmployeeByID]
@EmployeeID int = NULL
as
select * from View_Employees where EmployeeID=@EmployeeID


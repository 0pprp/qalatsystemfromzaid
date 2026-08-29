CREATE proc [dbo].[GetEmployeeAsyncID]
@EmployeeID int = NULL
as
select AsyncID from Employees where EmployeeID=@EmployeeID


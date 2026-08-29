CREATE proc [dbo].[GetEmployeeDebtByEmployee]
@EmployeeID int = NULL
as
select * from View_EmployeeDebts
where EmployeeID=@EmployeeID


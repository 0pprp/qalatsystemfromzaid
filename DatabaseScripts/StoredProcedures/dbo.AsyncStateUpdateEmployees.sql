
CREATE proc [dbo].[AsyncStateUpdateEmployees]
@EmployeeID int = NULL
as
update Employees set AsyncState='true' where EmployeeID=@EmployeeID


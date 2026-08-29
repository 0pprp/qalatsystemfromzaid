

CREATE proc [dbo].[UpdateEmployeeStateServer]
@EmployeeID int =null,
@EmployeeState bit = null
as
update Employees set EmployeeState=@EmployeeState where EmployeeID=@EmployeeID


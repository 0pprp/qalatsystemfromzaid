CREATE proc [dbo].[CheckFindEmployee]
@EmployeeName nvarchar(255)
as
select * from Employees where EmployeeName=@EmployeeName


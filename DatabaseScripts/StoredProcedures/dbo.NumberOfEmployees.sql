 
CREATE proc [dbo].[NumberOfEmployees]
as
select count(*) as NumberOfEmployees from  Employees where EmployeeState='true'



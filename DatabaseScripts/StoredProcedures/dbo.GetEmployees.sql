CREATE proc [dbo].[GetEmployees]
 
as
select * from View_Employees  where EmployeeState='true'


CREATE proc [dbo].[GetEmployeeByCity]
@CityID int = NULL
as
select * from View_Employees where EmployeeState='true' and CityID=@CityID


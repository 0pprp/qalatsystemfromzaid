CREATE proc [dbo].[DeleteDeleteEmployeesSalaryAsyncID]
@EmployeeSalaryID int = NULL
as
insert into DeleteData (EmployeesSalariesAsyncID) values ((select AsyncID from EmployeesSalaries where EmployeeSalaryID=@EmployeeSalaryID))


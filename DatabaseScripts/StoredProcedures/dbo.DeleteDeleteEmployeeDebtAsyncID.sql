CREATE proc [dbo].[DeleteDeleteEmployeeDebtAsyncID]
@EmployeeDebtsID int  = NULL
as
insert into DeleteData (EmployeeDebtsAsyncID) values ((select AsyncID from EmployeeDebts where EmployeeDebtsID=@EmployeeDebtsID))


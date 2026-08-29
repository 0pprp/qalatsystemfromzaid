
CREATE proc [dbo].[AsyncStateUpdateEmployeeDebts]
@EmployeeDebtsID int = NULL
as
update EmployeeDebts set AsyncState='true' where EmployeeDebtsID=@EmployeeDebtsID


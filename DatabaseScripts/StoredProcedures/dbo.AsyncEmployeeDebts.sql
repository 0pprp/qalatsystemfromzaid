CREATE proc [dbo].[AsyncEmployeeDebts]
as
select * from EmployeeDebts where AsyncState='false'


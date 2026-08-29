CREATE proc [dbo].[Employees_GetAll]
AS
BEGIN
    SELECT * FROM View_Employees where EmployeeState='true';
END



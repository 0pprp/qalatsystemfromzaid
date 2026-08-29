
CREATE PROCEDURE [dbo].[Employees_Delete]
    @EmployeeID INT,
    @UserDeleteID  INT
AS
BEGIN
    UPDATE Employees
    SET 
        EmployeeState = 0 
    WHERE EmployeeID = @EmployeeID;

	INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف الموظف '+(select EmployeeName from Employees where  EmployeeID=@EmployeeID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
END;



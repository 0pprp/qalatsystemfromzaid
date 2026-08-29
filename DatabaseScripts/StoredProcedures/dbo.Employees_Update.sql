
CREATE PROCEDURE [dbo].[Employees_Update]
    @EmployeeID INT,
    @UserUpdateID INT,
    @EmployeeName NVARCHAR(100),
    @Address NVARCHAR(255),
    @PhoneNumber NVARCHAR(50),
    @Notes NVARCHAR(255)
AS
BEGIN
    UPDATE Employees
    SET 
        EmployeeName = @EmployeeName,
        Address = @Address,
        PhoneNumber = @PhoneNumber,
        Notes = @Notes 
    WHERE EmployeeID = @EmployeeID;
    			INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل الموظف  '+@EmployeeName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
    SELECT * FROM View_Employees WHERE EmployeeID = @EmployeeID;
END;



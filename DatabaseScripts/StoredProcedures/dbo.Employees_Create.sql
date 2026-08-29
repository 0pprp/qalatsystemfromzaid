
CREATE PROCEDURE [dbo].[Employees_Create]
    @UserCreateID INT,
    @EmployeeName NVARCHAR(100),
    @Address NVARCHAR(255),
    @PhoneNumber NVARCHAR(50),
    @Notes NVARCHAR(255)
AS
BEGIN
    declare @DBName nvarchar(100) =  DB_NAME()
    declare @CityID int = (SELECT dbo.GetCityID(@DBName) AS CityID)
    INSERT INTO Employees (UserID,CityID , EmployeeName, Address, PhoneNumber, Notes,AsyncID,AsyncState,EmployeeState)
    VALUES (@UserCreateID,@CityID, @EmployeeName, @Address, @PhoneNumber, @Notes,NEWID(),0,1);


		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserCreateID
           ,N'تم اضافة الموظف '+@EmployeeName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
	DECLARE @LastId int;
	SET @LastId = IDENT_CURRENT('Employees');
    SELECT * FROM View_Employees WHERE EmployeeID =  @LastId
END;



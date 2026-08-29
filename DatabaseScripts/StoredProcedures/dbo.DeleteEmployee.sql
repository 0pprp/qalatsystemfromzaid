 CREATE proc [dbo].[DeleteEmployee]
 @EmployeeID int = NULL,
 @UserID int =null
 as
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف الموظف '+(select EmployeeName from Employees where EmployeeID=@EmployeeID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
 update Employees set EmployeeState='false'  where EmployeeID=@EmployeeID


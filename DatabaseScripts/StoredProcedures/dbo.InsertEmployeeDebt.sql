CREATE proc [dbo].[InsertEmployeeDebt]
@UserID int = NULL,
@EmployeeID int = NULL,
@AmountDebt float = NULL,
@DateDebt datetime = NULL,
@Purpose nvarchar(max) = NULL,
@Notes nvarchar(max) = NULL,
@AccountType nvarchar(255) = NULL
as
INSERT INTO [dbo].[EmployeeDebts]
           ([UserID]
           ,[EmployeeID]
           ,[AmountDebt]
           ,[DateDebt]
           ,[Purpose]
           ,[Notes]
           ,[AccountType]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@EmployeeID 
           ,@AmountDebt 
           ,@DateDebt 
           ,@Purpose 
           ,@Notes 
           ,@AccountType 
           ,'false' 
		   ,NEWID())
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة دين بمبلغ '+CONVERT(nvarchar(255),@AmountDebt*1448)+N' لنا للموظف '+(select EmployeeName from Employees where EmployeeID=@EmployeeID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


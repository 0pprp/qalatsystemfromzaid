CREATE proc [dbo].[DeleteEmployeeDebt]
@EmployeeDebtsID int = NULL,
@UserID int  = NULL
as
exec DeleteDeleteEmployeeDebtAsyncID @EmployeeDebtsID=@EmployeeDebtsID

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف دين الموظف '+(select EmployeeName from View_EmployeeDebts where EmployeeDebtsID=@EmployeeDebtsID)+N' بمبلغ '+(select CONVERT(nvarchar(255),AmountDebtDenar) from View_EmployeeDebts where EmployeeDebtsID=@EmployeeDebtsID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from EmployeeDebts
where  EmployeeDebtsID=@EmployeeDebtsID


 


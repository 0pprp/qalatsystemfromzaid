CREATE proc [dbo].[DeleteDelegatesDebt]
@DelegateDebtID int = NULL,
@UserID int = NULL
as
exec DeleteDelegatesDebtsAsyncID @DelegateDebtID=@DelegateDebtID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف من دين '+(select DelegateName from View_DelegatesDebts where DelegateDebtID=@DelegateDebtID)+N' محمد المبلغ '+(select AmountDebtDenar from View_DelegatesDebts where DelegateDebtID=@DelegateDebtID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from DelegatesDebts where DelegateDebtID=@DelegateDebtID

 


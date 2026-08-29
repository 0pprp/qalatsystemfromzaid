CREATE proc [dbo].[InsertDelegatesDebt]
@UserID int = NULL,
@DelegateID int = NULL,
@AmountDebt float = NULL,
@DateDebt datetime = NULL,
@Purpose nvarchar(max) = NULL,
@Notes nvarchar(max) = NULL,
@AccountType nvarchar(255) = NULL
as
 
INSERT INTO [dbo].[DelegatesDebts]
           ([UserID]
           ,[DelegateID]
           ,[AmountDebt]
           ,[DateDebt]
           ,[Purpose]
           ,[Notes]
           ,[AccountType]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@DelegateID 
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
           ,N'تم اضافة المبلغ '+@AmountDebt*1448+N' دين '+@AccountType+N' للمندوب '+(select DelegateName from Delegates where DelegateID=@DelegateID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


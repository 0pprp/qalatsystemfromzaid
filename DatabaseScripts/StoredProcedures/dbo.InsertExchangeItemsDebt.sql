CREATE proc [dbo].[InsertExchangeItemsDebt]
@UserID int = NULL,
@ExchangeItemID int = NULL,
@AmountDebt float = NULL,
@DateDebt datetime = NULL,
@Purpose nvarchar(max) = NULL,
@Notes nvarchar(max) = NULL,
@AccountType nvarchar(max) = NULL
as
 

INSERT INTO [dbo].[ExchangeItemsDebts]
           ([UserID]
           ,[ExchangeItemID]
           ,[AmountDebt]
           ,[DateDebt]
           ,[Purpose]
           ,[Notes]
           ,[AccountType]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@ExchangeItemID 
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
           ,N'تم اضافة الدين بمبلغ '+CONVERT(nvarchar(255),@AmountDebt*1448)+' '+@AccountType+N' الى بند الصرف '+(select ExchangeItemName from ExchangeItems where ExchangeItemID=@ExchangeItemID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


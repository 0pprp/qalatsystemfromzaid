CREATE proc [dbo].[DeleteExchangeItemsDebt]
@ExchangeItemDebtID int = NULL,
@UserID int = NULL
as
exec DeleteExchangeItemsDebtsAsyncID @ExchangeItemDebtID=@ExchangeItemDebtID

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف دين بند الصرف '+(select ExchangeItemName from View_ExchangeItemsDebts where ExchangeItemDebtID=@ExchangeItemDebtID)+N' المبلغ '+(select CONVERT(nvarchar(255),AmountDebtDenar) from View_ExchangeItemsDebts where ExchangeItemDebtID=@ExchangeItemDebtID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from ExchangeItemsDebts 
where  ExchangeItemDebtID=@ExchangeItemDebtID


CREATE proc [dbo].[InsertWithdrawalFromBoxFromBuy]
@BoxID int = NULL,
@Amount float= NULL,
@Purpose nvarchar(max)= NULL,
@Notes nvarchar(max)= NULL,
@UserID int = NULL,
@DateCreate datetime= NULL,
@BuyID int = NULL,
@SupplierID int = NULL
as
 
INSERT INTO [dbo].[WithdrawalFromBox]
           ([BoxID]
           ,[Amount]
           ,[Purpose]
           ,[Notes]
           ,[UserID]
           ,[DateCreate]
           ,[BuyID]
           ,[SupplierID]
           ,[AsyncState] 
		    ,[AsyncID])
     VALUES
           (@BoxID 
           ,@Amount 
           ,@Purpose 
           ,@Notes 
           ,@UserID 
           ,@DateCreate 
           ,@BuyID 
           ,@SupplierID 
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
           ,N'تم سحب المبلغ '+convert(nvarchar(255),@Amount*1448)+N' من الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+N' لغرض شراء '+(select ItemsNames from View_Buys where BuyID=@BoxID)+N' من المورد '+(select SupplierName from View_Buys where BuyID=@BoxID)+N' الى المخزن '+(select StoreName from View_Buys where BuyID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )


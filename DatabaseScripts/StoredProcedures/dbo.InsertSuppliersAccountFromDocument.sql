CREATE proc [dbo].[InsertSuppliersAccountFromDocument]
@SupplierID int = NULL,
@UserID int = NULL,
@Amount float= NULL,
@AccountType nvarchar(255)= NULL,
@AccountsDate datetime= NULL
as
 

INSERT INTO [dbo].[SuppliersAccounts]
           ([SupplierID]
           ,[UserID]
           ,[Amount]
           ,[AccountType]
           ,[AccountsDate]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@SupplierID 
           ,@UserID 
           ,@Amount 
           ,@AccountType 
           ,@AccountsDate 
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
           ,N'تم اضافة دين بمبلغ '+convert(nvarchar(255),@Amount*1448)+' '+@AccountType+N' من للمورد '+(select SupplierName from Suppliers where SupplierID=@SupplierID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


CREATE proc  [dbo].[AddCustomersPayment]
@UserID  int = NULL,
@CustomerID  int = NULL,
@BoxID  int = NULL,
@DelegateID  int = NULL,
@DateCreate datetime,
@Amount float,
@Notes nvarchar(max),
@FromAccount  nvarchar(255) ,
@ToAccount  nvarchar(255) ,
@DocumentType  nvarchar(255) 
as



 INSERT INTO [dbo].[CustomersPayments]
           ([UserID]
           ,[CustomerID]
           ,[BoxID]
           ,[PaymentDate]
           ,[BoundNumber]
           ,[DelegateID]
           ,[AccountZero]
           ,[DelegateState]
           ,[AsyncState]
           ,[AsyncID]
           ,[SelectState])
     VALUES
           (@UserID, 
            @CustomerID, 
            @BoxID, 
            @DateCreate, 
            (select count(*)+1 from CustomersPayments), 
            @DelegateID,  
            'false',  
            'true',  
            'false',  
            NEWID(), 
            'false')
 
INSERT INTO [dbo].[AddToBox]
           ([BoxID]
           ,[Amount]
           ,[Notes]
           ,[UserID]
           ,[DateCreate]
           ,[CustomerPaymentID]
           ,[AsyncState]
           ,[AsyncID] )
     VALUES
           (@BoxID,
		   @Amount,
		   @Notes,
		   @UserID,
		   @DateCreate,
		   (select top 1 CustomerPaymentID from CustomersPayments order by CustomerPaymentID desc ),
		   'false',
		    NEWID()
		   )
  

  
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تمت اضافة المبلغ '+@Amount*1448+N' من العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+N'  الى الصندوق '+(select BoxName from Boxes where BoxID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

		 


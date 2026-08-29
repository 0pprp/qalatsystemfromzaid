CREATE proc [dbo].[InsertCustomersPayment]
@UserID  int = NULL,
@CustomerID  int = NULL,
@DelegateID  int = NULL,
@DateCreate datetime = NULL,
@Amount float = NULL
as
DECLARE @BoxID INT =   (select BoxID from Delegates where DelegateID=@DelegateID)
INSERT INTO [dbo].[CustomersPayments]
           ([UserID]
           ,[CustomerID]
           ,[BoxID]
           ,[PaymentDate]
           ,[BoundNumber]
           ,[DelegateID]
           ,[AccountZero]
           ,[DelegateState]
           ,[Location]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID, 
            @CustomerID, 
            @BoxID, 
            @DateCreate, 
            (select count(*)+1 from CustomersPayments), 
            @DelegateID,  
            'false',  
            'true', 
			'0000,0000',
            'false',
			NEWID())
 
INSERT INTO [dbo].[AddToBox]
           ([BoxID]
           ,[Amount]
           ,[Notes]
           ,[UserID]
           ,[DateCreate]
           ,[CustomerPaymentID]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@BoxID,
		   @Amount,
		   N'قبض من الزبون '+(select CustomerName from Customers where CustomerID=@CustomerID)+'',
		   @UserID,
		   @DateCreate,
		   (select top 1 CustomerPaymentID from CustomersPayments order by CustomerPaymentID desc ),
		   'false' ,
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
           ,N'تم اضافة المبلغ '+CONVERT(nvarchar(255),@Amount*1448)+N' من الزبون '+(select CustomerName from Customers where CustomerID=@CustomerID)+N' الى الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


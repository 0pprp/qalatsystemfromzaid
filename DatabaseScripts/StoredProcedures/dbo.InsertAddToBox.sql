CREATE proc [dbo].[InsertAddToBox]
@BoxID int = NULL,
@Amount float = NULL,
@Notes nvarchar(max)= NULL,
@UserID int = NULL,
@SupplierID int = NULL,
@DelegateID int = NULL,
@DateCreate datetime = NULL,
@DateModify datetime =NULL,
@CustomerPaymentID int = NULL,
@EmployeeID int = NULL,
@DocumentID int = NULL,
@CustomerID int = NULL,
@TransferBoxID int = NULL
as
INSERT INTO [dbo].[AddToBox]
           ([BoxID]
           ,[Amount]
           ,[Notes]
           ,[UserID]
           ,[SupplierID]
           ,[DelegateID]
           ,[DateCreate]
           ,[DateModify]
           ,[CustomerPaymentID]
           ,[EmployeeID]
           ,[DocumentID]
           ,[CustomerID]
           ,[TransferBoxID]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@BoxID 
           ,@Amount 
           ,@Notes 
           ,@UserID 
           ,@SupplierID 
           ,@DelegateID 
           ,@DateCreate 
           ,@DateModify 
           ,@CustomerPaymentID 
           ,@EmployeeID 
           ,@DocumentID 
           ,@CustomerID 
           ,@TransferBoxID 
           ,'false'
           ,newID() )

			
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة المبلغ '+(@Amount*1448)+N' الى الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 


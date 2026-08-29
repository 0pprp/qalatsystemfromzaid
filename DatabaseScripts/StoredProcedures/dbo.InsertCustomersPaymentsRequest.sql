 
CREATE proc [dbo].[InsertCustomersPaymentsRequest]
@CustomerID int,
@DelegateID int,
@Amount float,
@Location nvarchar(255)
as
 
INSERT INTO [dbo].[CustomersPaymentsRequest]
           ([CustomerID]
           ,[PaymentDate]
           ,[DelegateID]
           ,[Amount]
           ,[Location])
     VALUES
           (@CustomerID,getdate(),@DelegateID,@Amount,@Location )
 



CREATE proc [dbo].[UpdateCustomerPaymentDate]
@CustomerPaymentID int = NULL,
@DateCreate datetime,
@UserID int = NULL
as 

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل  تاريخ التسديد '+(select AmountDenar from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' للعميل '+(select CustomerName from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' من التاريخ '+(select PaymentDate from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' الى تاريخ '+@DateCreate+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update CustomersPayments set PaymentDate=@DateCreate 
where CustomerPaymentID=@CustomerPaymentID


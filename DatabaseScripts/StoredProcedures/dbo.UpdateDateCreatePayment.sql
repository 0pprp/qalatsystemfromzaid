CREATE proc [dbo].[UpdateDateCreatePayment]
@CustomerPaymentID int = NULL,
@DateCreate datetime ,
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
           ,N'تم تغيير تاريخ التسديد '+(select CONVERT(nvarchar(255),AmountDenar) from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' التابع للعميل '+(select CustomerName from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' من التاريخ '+(select CONVERT(nvarchar(255),PaymentDate) from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' الى التاريخ '+ CONVERT(nvarchar(255),@DateCreate)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

update CustomersPayments set PaymentDate=@DateCreate
where CustomerPaymentID=@CustomerPaymentID


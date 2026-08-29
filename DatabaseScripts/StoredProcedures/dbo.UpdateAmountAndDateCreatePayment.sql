 
CREATE proc [dbo].[UpdateAmountAndDateCreatePayment]
@CustomerPaymentID int = NULL,
@DateCreate datetime ,
@Amount float ,
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
           ,N'تم تغيير تاريخ التسديد '+(select CONVERT(nvarchar(255),AmountDenar) from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' التابع للعميل '+(select CustomerName from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' من التاريخ '+(select CONVERT(nvarchar(255),PaymentDate) from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' الى التاريخ '+ CONVERT(nvarchar(255),@DateCreate)+N' وتم تغيير مبلغ التسديد من '+(select CONVERT(nvarchar(255),AmountDenar) from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' الى '+CONVERT(nvarchar(255),@Amount*1448)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

update CustomersPayments set PaymentDate=@DateCreate
where CustomerPaymentID=@CustomerPaymentID
DECLARE @AddToBoxID int=(select  top 1 AddToBoxID from AddToBox where CustomerPaymentID=@CustomerPaymentID);
update AddToBox set Amount =@Amount,DateCreate=@DateCreate where AddToBoxID=@AddToBoxID


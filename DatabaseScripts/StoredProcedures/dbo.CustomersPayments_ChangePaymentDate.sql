
CREATE PROCEDURE [dbo].[CustomersPayments_ChangePaymentDate]
    @Paymentlist NVARCHAR(MAX),  
    @NewDate DATETIME,
    @UserID INT
AS
BEGIN
    UPDATE CustomersPayments
    SET PaymentDate = @NewDate
    WHERE CustomerPaymentID IN (
        SELECT Value FROM dbo.SplitString(@Paymentlist, ',')
    );

    UPDATE AddToBox
    SET DateCreate = @NewDate
    WHERE CustomerPaymentID IN (
        SELECT Value FROM dbo.SplitString(@Paymentlist, ',')
    );

		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل تسديدات محددة'
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
END



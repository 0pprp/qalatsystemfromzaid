
CREATE PROCEDURE [dbo].[CustomersPayments_DeleteSelect]
    @Paymentlist NVARCHAR(MAX),  
    @UserID INT
AS
BEGIN
	DELETE FROM AddToBox  WHERE CustomerPaymentID IN (
        SELECT Value FROM dbo.SplitString(@Paymentlist, ',')
    );

	DELETE FROM CustomersPayments  WHERE CustomerPaymentID IN (
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
           ,N'تم حذف تسديدات محددة'
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
END



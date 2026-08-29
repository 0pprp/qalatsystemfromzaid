create PROCEDURE [dbo].[CustomersPaymentsRequest_DeleteSelect]
    @Paymentlist NVARCHAR(MAX),  
    @UserID INT
AS
BEGIN
	DELETE FROM CustomersPaymentsRequest  WHERE CustomersPaymentsRequestID IN (
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
           ,N'تم حذف طلبات تسديدات محددة'
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
END



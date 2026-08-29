
CREATE procEDURE [dbo].[InsertServerDelegates]
    @UserID INT = NULL,
    @CityID INT = NULL,
    @DelegateName NVARCHAR(255)= NULL,
    @Address NVARCHAR(255)= NULL,
    @PhoneNumber NVARCHAR(255)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @DelegateImage NVARCHAR(MAX)= NULL,
    @DelegateState BIT = NULL,
    @ProfitRatio FLOAT= NULL,
    @SelectState BIT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @BoxID INT = NULL,
    @BoxBalanceID INT = NULL,
    @BalanceSaleState BIT = NULL,
    @DeviceSaleState BIT = NULL,
    @BalancePaymentState BIT = NULL,
    @DevicePaymentState BIT = NULL,
    @ReceiptName NVARCHAR(255)= NULL,
    @UpdateReceipt BIT = NULL,
    @DeleteReceipt BIT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Delegates]
           ([UserID]
           ,[CityID]
           ,[DelegateName]
           ,[Address]
           ,[PhoneNumber]
           ,[Notes]
           ,[DelegateImage]
           ,[DelegateState]
           ,[ProfitRatio]
           ,[SelectState]
           ,[AsyncState]
           ,[AsyncID]
           ,[BoxID]
           ,[BoxBalanceID]
           ,[BalanceSaleState]
           ,[DeviceSaleState]
           ,[BalancePaymentState]
           ,[DevicePaymentState]
           ,[ReceiptName]
           ,[UpdateReceipt]
           ,[DeleteReceipt])
     VALUES
           (@UserID
           ,@CityID
           ,@DelegateName
           ,@Address
           ,@PhoneNumber
           ,@Notes
           ,@DelegateImage
           ,@DelegateState
           ,@ProfitRatio
           ,@SelectState
           ,@AsyncState
           ,@AsyncID
           ,@BoxID
           ,@BoxBalanceID
           ,@BalanceSaleState
           ,@DeviceSaleState
           ,@BalancePaymentState
           ,@DevicePaymentState
           ,@ReceiptName
           ,@UpdateReceipt
           ,@DeleteReceipt)
END


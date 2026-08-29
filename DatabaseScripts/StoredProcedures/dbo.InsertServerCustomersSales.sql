

CREATE procEDURE [dbo].[InsertServerCustomersSales]
    @UserID INT = NULL,
    @CustomerID INT = NULL,
    @Notes NVARCHAR(MAX) =NULL,
    @DateCreate DATETIME = NULL,
    @DateModify datetime =NULL,
    @BoundNumber INT = NULL,
    @StoreID INT = NULL,
    @DelegateID INT = NULL,
    @AccountZero BIT = NULL,
    @DelegateState BIT = NULL,
    @DiscountAmountTotal FLOAT = NULL,
    @DiscountAmountTotalDay FLOAT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @DiscountAmountTotalTwoWay FLOAT= NULL ,
    @DiscountAmountDayTotalTwoWay FLOAT = NULL,
    @MerchantID int = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[CustomersSales]
           ([UserID]
           ,[CustomerID]
           ,[Notes]
           ,[DateCreate]
           ,[DateModify]
           ,[BoundNumber]
           ,[StoreID]
           ,[DelegateID]
           ,[AccountZero]
           ,[DelegateState]
           ,[DiscountAmountTotal]
           ,[DiscountAmountTotalDay]
           ,[AsyncState]
           ,[AsyncID]
           ,[DiscountAmountTotalTwoWay]
           ,[DiscountAmountDayTotalTwoWay]
           ,[MerchantID])
     VALUES
           (@UserID
           ,@CustomerID
           ,@Notes
           ,@DateCreate
           ,@DateModify
           ,@BoundNumber
           ,@StoreID
           ,@DelegateID
           ,@AccountZero
           ,@DelegateState
           ,@DiscountAmountTotal
           ,@DiscountAmountTotalDay
           ,@AsyncState
           ,@AsyncID
           ,@DiscountAmountTotalTwoWay
           ,@DiscountAmountDayTotalTwoWay
           ,@MerchantID)
END


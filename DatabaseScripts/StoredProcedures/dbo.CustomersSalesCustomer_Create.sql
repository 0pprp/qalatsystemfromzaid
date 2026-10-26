CREATE OR ALTER PROC [dbo].[CustomersSalesCustomer_Create]
@CustomerName nvarchar(100),
@PhoneNumber nvarchar(100),
@Address nvarchar(100),
@ShopName nvarchar(100),
@NearestFunctionPoint nvarchar(100),
@SaleName nvarchar(100),
@ReceiptName nvarchar(100),
@Notes nvarchar(max),
@UserID int = NULL,
@DateCreate datetime = NULL,
@StoreID int = NULL,
@DelegateID int = NULL,
@DiscountAmountTotal float = NULL,
@DiscountAmountTotalDay float  = NULL
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;

  DECLARE @DBName nvarchar(100) = DB_NAME();
  DECLARE @CityID int = (SELECT dbo.GetCityID(@DBName) AS CityID);
  DECLARE @CustomerID int;
  DECLARE @CustomerSaleID int;
  DECLARE @WithdrawalStoresID int;
  DECLARE @BoundNumber int;

  BEGIN TRY
    BEGIN TRAN;

    INSERT INTO Customers (
      DelegateID, CityID, CustomerName, PhoneNumber, Address, ShopName,
      NearestFunctionPoint, SaleName, ReceiptName, Notes, UserID, AsyncID, AsyncState, CustomerState)
    VALUES (
      @DelegateID, @CityID, @CustomerName, @PhoneNumber, @Address, @ShopName,
      @NearestFunctionPoint, @SaleName, @ReceiptName, @Notes, @UserID, NEWID(), 0, 1);

    SET @CustomerID = CAST(SCOPE_IDENTITY() AS int);
    IF @CustomerID IS NULL OR @CustomerID <= 0
      THROW 50001, N'Customers insert did not return identity.', 1;

    -- Serialize BoundNumber allocation for this session/table to avoid count(*)+1 races.
    SELECT @BoundNumber = ISNULL(MAX(BoundNumber), 0) + 1
    FROM dbo.CustomersSales WITH (UPDLOCK, HOLDLOCK);

    INSERT INTO [dbo].[CustomersSales]
           ([UserID],[CustomerID],[DateCreate],[BoundNumber],[StoreID],[DelegateID],
            [AccountZero],[DelegateState],[DiscountAmountTotal],[DiscountAmountTotalDay],[AsyncState],[AsyncID])
     VALUES
           (@UserID,@CustomerID,@DateCreate,@BoundNumber,@StoreID,@DelegateID,
            'false','false',@DiscountAmountTotal,@DiscountAmountTotalDay,'false',NEWID());

    SET @CustomerSaleID = CAST(SCOPE_IDENTITY() AS int);
    IF @CustomerSaleID IS NULL OR @CustomerSaleID <= 0
      THROW 50002, N'CustomersSales insert did not return identity.', 1;

    EXEC InsertWithdrawalStores @UserID=@UserID, @State='true', @WithdrawalStoresDate=@DateCreate, @StoreID=@StoreID;
    -- Nested proc identity: @@IDENTITY (session) is required after EXEC; SCOPE_IDENTITY is outer-scope only.
    SET @WithdrawalStoresID = CAST(@@IDENTITY AS int);

    EXEC InsertSelectItemsSaleFromSelectItemSalesTemporary
      @CustomerSaleID=@CustomerSaleID,
      @WithdrawalStoresID=@WithdrawalStoresID,
      @UserID=@UserID;
    EXEC ClearSelectItemSalesTemporary @UserID=@UserID;

    INSERT INTO [dbo].[Activities]
           ([UserID],[ActivityDescription],[ActivityDate],[AsyncState],[AsyncID])
     VALUES
           (@UserID,
            N'?? ??? '+(SELECT TOP 1 ItemsNames FROM View_CustomersSales WHERE CustomerSaleID = @CustomerSaleID)+
            N'  ??? ?????? '+(SELECT TOP 1 CustomerName FROM View_CustomersSales WHERE CustomerSaleID = @CustomerSaleID),
            GETUTCDATE(),
            'false',
            NEWID());

    COMMIT TRAN;

    SELECT * FROM View_CustomersDelegate WHERE CustomerID = @CustomerID;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
  END CATCH
END

-- Safe Buys_Create: SCOPE_IDENTITY + transaction + serialized BoundNumber.
-- Apply on isolated branch DB only. Do not run on production without reconciliation plan.
CREATE OR ALTER PROC [dbo].[Buys_Create]
@UserID int = NULL,
@SupplierID int = NULL,
@DateCreate datetime,
@StoreID int = NULL,
@BoxID int = NULL,
@AmountPaidDenar float = NULL,
@FinalAmountTotalDenar float = NULL,
@AmountTotalDenar float = NULL,
@RemainingAmountDenar float = NULL
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;
  BEGIN TRY
    BEGIN TRAN;

    DECLARE @BoundNumber int =
      (SELECT ISNULL(MAX(BoundNumber), 0) + 1
       FROM dbo.Buys WITH (UPDLOCK, HOLDLOCK));

    INSERT INTO [dbo].[Buys]
           ([UserID],[SupplierID],[BoundNumber],[DateCreate],[StoreID],[BoxID],[AsyncID],AsyncState,BuyState)
     VALUES
           (@UserID,@SupplierID,@BoundNumber,@DateCreate,@StoreID,@BoxID,NEWID(),'false','true');

    DECLARE @BuyID int = CAST(SCOPE_IDENTITY() AS int);
    IF @BuyID IS NULL OR @BuyID <= 0
      THROW 50010, N'Buys insert did not return identity.', 1;

    DECLARE @Purpose nvarchar(max) = N'شراء '+(SELECT TOP 1 ItemsNames FROM View_Buys WHERE BuyID=@BuyID);
    DECLARE @Notes nvarchar(max) = N'لا يوجد';
    DECLARE @AmountAccount float = @FinalAmountTotalDenar-@AmountTotalDenar;
    DECLARE @FromAccount nvarchar(255) = N'من حساب الخزينة '+(SELECT BoxName FROM Boxes WHERE BoxID=@BoxID);
    DECLARE @ToAccount nvarchar(255) = N'الى حساب المورد '+(SELECT SupplierName FROM Suppliers WHERE SupplierID=@SupplierID);

    EXEC InsertBuysItemsFromSelectItemBuyTemporary @BuyID=@BuyID,@UserID=@UserID;
    EXEC InsertWithdrawalFromBoxFromBuy @BoxID=@BoxID,@Amount=@AmountPaidDenar,@Purpose=@Purpose,@Notes=@Notes,@UserID=@UserID,@DateCreate=@DateCreate,@BuyID=@BuyID,@SupplierID=@SupplierID;
    EXEC InsertSuppliersAccount @SupplierID=@SupplierID,@UserID=@UserID,@BuyID=@BuyID,@Amount=@AmountAccount,@AccountType=N'لنا',@AccountsDate=@DateCreate;
    EXEC InsertSuppliersAccount @SupplierID=@SupplierID,@UserID=@UserID,@BuyID=@BuyID,@Amount=@RemainingAmountDenar,@AccountType=N'علينا',@AccountsDate=@DateCreate;
    EXEC InsertDocument @UserID=@UserID,@FromAccount=@FromAccount,@ToAccount=@ToAccount,@DocumentDateCreate=@DateCreate,@Notes=@Notes,@DocumentType=N'سند صرف',@Amount=@AmountPaidDenar;
    EXEC InsertAddToStore @UserID=@UserID, @DateAddToStore=@DateCreate, @StoreID=@StoreID;

    DECLARE @AddToStoreID int = CAST(@@IDENTITY AS int);
    EXEC InsertSelectItemsAddToStoresFromSelectItemBuyTemporary @AddToStoreID=@AddToStoreID,@UserID=@UserID;
    DELETE FROM SelectItemBuyTemporary WHERE UserID=@UserID;

    INSERT INTO [dbo].[Activities]
           ([UserID],[ActivityDescription],[ActivityDate],[AsyncState],[AsyncID])
     VALUES
           (@UserID,
            N'تم شراء العناصر '+(SELECT TOP 1 ItemsNames FROM View_Buys WHERE BuyID=@BuyID)+N' من المورد '+(SELECT TOP 1 SupplierName FROM View_Buys WHERE BuyID=@BuyID)+N' الى المخزن '+(SELECT TOP 1 StoreName FROM View_Buys WHERE BuyID=@BuyID)+' ',
            GETUTCDATE(),'false',NEWID());

    COMMIT TRAN;
    SELECT * FROM View_Buys WHERE BuyID=@BuyID;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
  END CATCH
END
GO

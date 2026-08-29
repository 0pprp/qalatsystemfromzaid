CREATE proc [dbo].[InsertSelectItemsSaleFromSelectItemSalesTemporary]
@CustomerSaleID int = NULL,
@WithdrawalStoresID int = NULL,
@UserID int =null
 as
DECLARE @SelectItemSalesTemporaryID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT SelectItemSalesTemporaryID
FROM SelectItemSalesTemporary  where UserID=@UserID
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @SelectItemSalesTemporaryID;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @ItemID INT = (select ItemID from SelectItemSalesTemporary where SelectItemSalesTemporaryID=@SelectItemSalesTemporaryID);
    DECLARE @Quantity INT = (select Quantity from SelectItemSalesTemporary where SelectItemSalesTemporaryID=@SelectItemSalesTemporaryID);
	exec InsertSelectItemsSales @UserID=@UserID,@CustomerSaleID=@CustomerSaleID,@ItemID=@ItemID,@Quantity=@Quantity
	exec InsertSelectItemsWithdrawal @UserID=@UserID,@WithdrawalStoresID=@WithdrawalStoresID,@ItemID=@ItemID,@Quantity=@Quantity
	update Items set Quantity=Quantity-@Quantity where ItemID=@ItemID
    FETCH NEXT FROM cursor_name INTO @SelectItemSalesTemporaryID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


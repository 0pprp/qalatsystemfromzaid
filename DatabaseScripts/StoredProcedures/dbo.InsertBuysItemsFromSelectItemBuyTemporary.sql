
CREATE proc [dbo].[InsertBuysItemsFromSelectItemBuyTemporary]
@BuyID int = NULL,
@UserID int =null
 as
DECLARE @SelectItemBuyTemporaryID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT SelectItemBuyTemporaryID
FROM SelectItemBuyTemporary  where UserID=@UserID
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @SelectItemBuyTemporaryID;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @ItemID INT = (select ItemID from SelectItemBuyTemporary where SelectItemBuyTemporaryID=@SelectItemBuyTemporaryID);
    DECLARE @Quantity INT = (select Quantity from SelectItemBuyTemporary where SelectItemBuyTemporaryID=@SelectItemBuyTemporaryID);
    exec InsertBuysItems @UserID=@UserID,@BuyID=@BuyID,@ItemID=@ItemID,@Quantity=@Quantity
	update Items set Quantity=Quantity+@Quantity where ItemID=@ItemID
    FETCH NEXT FROM cursor_name INTO @SelectItemBuyTemporaryID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


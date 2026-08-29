
 CREATE proc [dbo].[InsertSelectItemsAddToStoresFromSelectItemBuyTemporary]
@AddToStoreID int = NULL,
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
    exec InsertSelectItemsAddToStores @AddToStoreID=@AddToStoreID,@UserID=@UserID,@ItemID=@ItemID,@Quantity=@Quantity
    FETCH NEXT FROM cursor_name INTO @SelectItemBuyTemporaryID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


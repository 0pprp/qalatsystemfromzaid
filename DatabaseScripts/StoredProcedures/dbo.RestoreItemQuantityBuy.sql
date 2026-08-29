
CREATE proc [dbo].[RestoreItemQuantityBuy]
@BuyID int  = NULL
as
DECLARE @BuyItemID VARCHAR(50);
DECLARE cursor_name CURSOR FOR
SELECT BuyItemID
FROM BuysItems  where BuyID=@BuyID;;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @BuyItemID;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @Quantity int = (select Quantity from BuysItems where BuyItemID=@BuyItemID);
    DECLARE @ItemID int = (select ItemID from BuysItems where BuyItemID=@BuyItemID);
    update Items set Quantity=Quantity-@Quantity where ItemID=@ItemID
	update Items set Quantity=0 where Quantity<0
    FETCH NEXT FROM cursor_name INTO @BuyItemID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


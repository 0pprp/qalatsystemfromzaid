CREATE proc [dbo].[RestoreItemQuantitySale]
@CustomerSaleID int = NULL
as
DECLARE @SelectItemsSaleID int;
DECLARE cursor_name CURSOR FOR
SELECT SelectItemsSaleID
FROM SelectItemsSales  where CustomerSaleID=@CustomerSaleID;;
OPEN cursor_name;
FETCH NEXT FROM cursor_name INTO @SelectItemsSaleID;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @ItemID int=(select ItemID from SelectItemsSales where SelectItemsSaleID=@SelectItemsSaleID)
    DECLARE @Quantity int=(select Quantity from SelectItemsSales where SelectItemsSaleID=@SelectItemsSaleID)
	update Items set Quantity=Quantity+@Quantity where ItemID=@ItemID
    FETCH NEXT FROM cursor_name INTO @SelectItemsSaleID;
END
CLOSE cursor_name;
DEALLOCATE cursor_name;


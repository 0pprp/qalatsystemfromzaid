CREATE proc [dbo].[MinQuantitySelectItemSalesTemporary]
@SelectItemSalesTemporaryID int = NULL
as
update SelectItemSalesTemporary set  Quantity=Quantity-1
where SelectItemSalesTemporaryID=@SelectItemSalesTemporaryID


CREATE proc [dbo].[PlusQuantitySelectItemSalesTemporary]
@SelectItemSalesTemporaryID int = NULL
as
update SelectItemSalesTemporary set  Quantity=Quantity+1
where SelectItemSalesTemporaryID=@SelectItemSalesTemporaryID


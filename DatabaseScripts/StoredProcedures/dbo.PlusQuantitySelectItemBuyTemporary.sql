CREATE proc [dbo].[PlusQuantitySelectItemBuyTemporary]
@SelectItemBuyTemporaryId int = NULL
as
update SelectItemBuyTemporary set Quantity=Quantity+1
where SelectItemBuyTemporaryId=@SelectItemBuyTemporaryId


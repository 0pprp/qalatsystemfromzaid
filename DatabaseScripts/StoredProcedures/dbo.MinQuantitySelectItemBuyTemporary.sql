CREATE proc [dbo].[MinQuantitySelectItemBuyTemporary]
@SelectItemBuyTemporaryId int = NULL
as
update SelectItemBuyTemporary set Quantity=Quantity-1
where SelectItemBuyTemporaryId=@SelectItemBuyTemporaryId


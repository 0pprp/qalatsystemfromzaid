CREATE proc [dbo].[GetSelectItemSalesTemporaryByUserItemNameCheckItemId]
@UserID int = NULL,
@ItemID int = NULL
as
select * from SelectItemSalesTemporary where UserID=@UserID and ItemID=@ItemID


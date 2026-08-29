CREATE proc [dbo].[GetSelectItemBuyTemporaryByUserItemNameCheckItemId]
@UserID int = NULL,
@ItemID int = NULL
as 
select * from SelectItemBuyTemporary
where UserID=@UserID and 
ItemID=@ItemID


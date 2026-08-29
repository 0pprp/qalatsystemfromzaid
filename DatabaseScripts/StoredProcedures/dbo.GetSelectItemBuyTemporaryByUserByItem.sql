CREATE proc [dbo].[GetSelectItemBuyTemporaryByUserByItem]
@UserID int = NULL,
@ItemID int = NULL
as
select * from View_SelectItemBuyTemporary where UserID=@UserID and ItemID=@ItemID


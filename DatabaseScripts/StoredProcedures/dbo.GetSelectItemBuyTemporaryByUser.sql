CREATE proc [dbo].[GetSelectItemBuyTemporaryByUser]
@UserID int = NULL
as
select * from View_SelectItemBuyTemporary
where UserID=@UserID


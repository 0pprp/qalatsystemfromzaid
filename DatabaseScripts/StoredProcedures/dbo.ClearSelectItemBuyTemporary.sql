CREATE proc [dbo].[ClearSelectItemBuyTemporary]
@UserID int = NULL
as delete from SelectItemBuyTemporary 
where UserID=@UserID


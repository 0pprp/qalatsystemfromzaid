CREATE proc [dbo].[ClearSelectItemSalesTemporary]
@UserID int = NULL
as
delete from SelectItemSalesTemporary where UserID=@UserID


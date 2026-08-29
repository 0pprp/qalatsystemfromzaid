CREATE proc [dbo].[GetSelectItemSalesTemporaryByUserID]
@UserID int = NULL
as
select * from View_SelectItemSalesTemporary where UserID=@UserID


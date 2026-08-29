CREATE proc [dbo].[GetSelectItemSalesTemporaryByUser]
@UserID int = NULL
as
select * from View_SelectItemSalesTemporary
where UserID=@UserID


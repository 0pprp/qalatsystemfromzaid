CREATE proc [dbo].[GetSelectItemsAddToStoresTemporaryByUser]
@UserID int = NULL
as
select * from View_SelectItemsAddToStoresTemporary
where UserID=@UserID


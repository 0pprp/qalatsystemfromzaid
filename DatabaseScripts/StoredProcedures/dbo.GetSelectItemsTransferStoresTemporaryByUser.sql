CREATE proc [dbo].[GetSelectItemsTransferStoresTemporaryByUser]
@UserID int = NULL
as
select * from View_SelectItemsTransferStoresTemporary
where UserID=@UserID

 


CREATE proc [dbo].[GetSelectItemsWithdrawalTemporaryByUser]
@UserID int = NULL
as
select * from View_SelectItemsWithdrawalTemporary
where UserID=@UserID


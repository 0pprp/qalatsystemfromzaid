CREATE proc [dbo].[GetSelectItemDamageTemporaryByUser]
@UserID int = NULL
as
select * from View_SelectItemDamageTemporary
where UserID=@UserID


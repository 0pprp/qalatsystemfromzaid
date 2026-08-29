CREATE proc [dbo].[DeleteSelectItemDamageTemporary]
@SelectItemDamageTemporaryID int = NULL
as
delete from SelectItemDamageTemporary
where SelectItemDamageTemporaryID=@SelectItemDamageTemporaryID


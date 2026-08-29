CREATE proc [dbo].[DeleteSelectItemSalesTemporary]
@SelectItemSalesTemporaryID int = NULL
as
delete from SelectItemSalesTemporary
where SelectItemSalesTemporaryID=@SelectItemSalesTemporaryID


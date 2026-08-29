CREATE proc [dbo].[DeleteSelectItemBuyTemporary]
@SelectItemBuyTemporaryID int = NULL
as
delete from SelectItemBuyTemporary where 
SelectItemBuyTemporaryID=@SelectItemBuyTemporaryID


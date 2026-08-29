CREATE proc [dbo].[DeleteSelectItemsWithdrawalTemporary]
@SelectItemWithdrawalID int

as
delete from SelectItemsWithdrawalTemporary
where SelectItemWithdrawalID=@SelectItemWithdrawalID


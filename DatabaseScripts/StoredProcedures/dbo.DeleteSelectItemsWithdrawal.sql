CREATE proc [dbo].[DeleteSelectItemsWithdrawal]
@SelectItemWithdrawalID  int  = NULL
as
exec DeleteOneSelectItemsWithdrawalAsyncID @SelectItemWithdrawalID=@SelectItemWithdrawalID
delete from SelectItemsWithdrawal
where  SelectItemWithdrawalID=@SelectItemWithdrawalID


 


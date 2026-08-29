CREATE proc [dbo].[DeleteOneSelectItemsWithdrawalAsyncID]
@SelectItemWithdrawalID int = NULL
as
insert into DeleteData (SelectItemsWithdrawalAsyncID)
values 
((select AsyncID from SelectItemsWithdrawal where SelectItemWithdrawalID=@SelectItemWithdrawalID))


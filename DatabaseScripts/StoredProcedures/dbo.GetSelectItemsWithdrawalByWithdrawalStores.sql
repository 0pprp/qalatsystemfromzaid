CREATE proc [dbo].[GetSelectItemsWithdrawalByWithdrawalStores]
@WithdrawalStoresID int = NULL
as
select * from View_SelectItemsWithdrawal
where WithdrawalStoresID=@WithdrawalStoresID

 


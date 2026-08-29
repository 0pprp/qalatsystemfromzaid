CREATE proc [dbo].[GetWithdrawalStoresAsyncID]
@WithdrawalStoresID int = NULL
as
select AsyncID from WithdrawalStores where WithdrawalStoresID=@WithdrawalStoresID


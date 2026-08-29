CREATE proc [dbo].[DeleteWithdrawalStores]
@WithdrawalStoresID int  = NULL
as
delete from WithdrawalStores where WithdrawalStoresID=@WithdrawalStoresID


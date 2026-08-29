
CREATE proc [dbo].[AsyncStateUpdateWithdrawalStores]
@WithdrawalStoresID int = NULL
as
update WithdrawalStores set AsyncState='true' where WithdrawalStoresID=@WithdrawalStoresID


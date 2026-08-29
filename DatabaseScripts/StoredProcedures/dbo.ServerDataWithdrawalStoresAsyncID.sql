CREATE proc [dbo].[ServerDataWithdrawalStoresAsyncID]
@AsyncID nvarchar(255) = null
as
select top 1 * from WithdrawalStores where AsyncID=@AsyncID


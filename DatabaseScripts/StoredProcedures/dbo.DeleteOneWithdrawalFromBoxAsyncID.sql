CREATE proc [dbo].[DeleteOneWithdrawalFromBoxAsyncID]
@WithdrawalFromBoxID int = NULL
as
insert into DeleteData (WithdrawalFromBoxAsyncID) values 
((select AsyncID from WithdrawalFromBox where WithdrawalFromBoxID=@WithdrawalFromBoxID))


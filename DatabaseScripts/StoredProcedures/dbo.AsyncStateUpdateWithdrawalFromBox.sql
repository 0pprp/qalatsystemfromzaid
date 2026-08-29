
CREATE proc [dbo].[AsyncStateUpdateWithdrawalFromBox]
@WithdrawalFromBoxID int = NULL
as
update WithdrawalFromBox set AsyncState='true' where WithdrawalFromBoxID=@WithdrawalFromBoxID


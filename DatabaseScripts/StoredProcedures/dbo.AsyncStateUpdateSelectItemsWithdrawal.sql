
CREATE proc [dbo].[AsyncStateUpdateSelectItemsWithdrawal]
@SelectItemWithdrawalID int = NULL
as
update SelectItemsWithdrawal set AsyncState='true' where SelectItemWithdrawalID=@SelectItemWithdrawalID


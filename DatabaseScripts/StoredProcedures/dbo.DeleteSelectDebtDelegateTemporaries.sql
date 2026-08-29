CREATE proc [dbo].[DeleteSelectDebtDelegateTemporaries]
@SelectDebtDelegateTemporaryID int = NULL
as
delete from SelectDebtDelegateTemporaries
where  SelectDebtDelegateTemporaryID=@SelectDebtDelegateTemporaryID


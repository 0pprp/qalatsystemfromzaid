CREATE proc [dbo].[GetDelegatesDebtByDelegate]
@DelegateID int = NULL
as
select * from View_DelegatesDebts
where DelegateID=@DelegateID


CREATE proc [dbo].[GetWithdrawalFromBoxByBox]
@BoxID int = NULL
as
select * from View_WithdrawalFromBox
where BoxID=@BoxID


CREATE proc [dbo].[GetDelegateTitle]
@DelegateID int
as
select * from View_Delegates where DelegateID=@DelegateID

